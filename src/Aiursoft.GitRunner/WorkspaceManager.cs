using System.Text;
using Aiursoft.Canon;
using Aiursoft.CSTools.Tools;
using Aiursoft.GitRunner.Exceptions;
using Aiursoft.GitRunner.Models;
using Aiursoft.GitRunner.Services;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Logging;

// ReSharper disable InconsistentNaming

namespace Aiursoft.GitRunner;

/// <summary>
///     Workspace initializer.
/// </summary>
public class WorkspaceManager(
    ILogger<WorkspaceManager> logger,
    RetryEngine retryEngine,
    GitCommandRunner gitCommandRunner)
    : ITransientDependency
{
    /// <summary>
    ///     Get current branch from a git repo.
    /// </summary>
    /// <param name="path">Path</param>
    /// <returns>Current branch.</returns>
    public async Task<string> GetBranch(string path)
    {
        var gitBranchOutput = await gitCommandRunner.RunGit(path, "rev-parse --abbrev-ref HEAD");
        return gitBranchOutput
            .Split('\n')
            .Single(s => !string.IsNullOrWhiteSpace(s))
            .Trim();
    }

    /// <summary>
    /// Get all commit times from a git repo. Response array is descending.
    /// </summary>
    /// <param name="path">Path</param>
    /// <returns>Datetime array</returns>
    public async Task<DateTime[]> GetCommitTimes(string path)
    {
        try
        {
            var gitCommitsOutput = await gitCommandRunner.RunGit(path, "--no-pager log --format=%at");
            var lines = gitCommitsOutput.Split('\n');
            var times = new List<DateTime>();

            foreach (var commitTime in lines)
            {
                if (long.TryParse(commitTime, out var unixTime))
                {
                    var time = DateTime.UnixEpoch.AddSeconds(unixTime);
                    times.Add(time);
                }
            }

            return times.ToArray();
        }
        catch (GitCommandException ex)
        {
            // Check for the 'no commits' type of error
            if (ex.Message.Contains("does not have any commits yet"))
            {
                // Return an empty array to avoid further errors
                return [];
            }

            // Otherwise rethrow so other errors bubble up
            throw;
        }
    }

    public async Task<Commit[]> GetCommits(string path)
    {
        var commits = new List<Commit>();
        var gitCommitsOutput = await gitCommandRunner.RunGit(path, "--no-pager log --format=%an%n%ae%n%s%n%at%n%H");
        var lines = gitCommitsOutput.Split('\n');
        for (var i = 0; i + 4 < lines.Length; i += 5)
        {
            var commit = new Commit
            {
                Author = lines[i],
                Email = lines[i + 1],
                Message = lines[i + 2],
                Time = DateTime.UnixEpoch.AddSeconds(long.Parse(lines[i + 3])),
                Hash = lines[i + 4]
            };
            commits.Add(commit);
        }

        return commits.ToArray();
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public async Task SwitchToBranch(string sourcePath, string targetBranch, bool enforceCurrentContent)
    {
        var currentBranch = await GetBranch(sourcePath);
        if (string.Equals(currentBranch, targetBranch, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            await gitCommandRunner.RunGit(sourcePath, $"checkout -b {targetBranch}");
        }
        catch (GitCommandException e) when (e.Message.Contains("already exists"))
        {
            if (enforceCurrentContent)
            {
                await gitCommandRunner.RunGit(sourcePath, $"branch -D {targetBranch}");
                await SwitchToBranch(sourcePath, targetBranch, enforceCurrentContent);
            }
            else
            {
                await gitCommandRunner.RunGit(sourcePath, $"checkout {targetBranch}");
            }
        }
    }

    public async Task Init(string path)
    {
        await gitCommandRunner.RunGit(path, "init");
    }

    public async Task AddAndCommit(string path, string message)
    {
        await gitCommandRunner.RunGit(path, "add .");
        if (!string.IsNullOrWhiteSpace(await GetCurrentUserEmail(path)))
        {
            await gitCommandRunner.RunGit(path, $@"commit -m ""{message}""");
        }
        else
        {
            await gitCommandRunner.RunGit(path, $@"config user.email ""nobody@domain.com""");
            await gitCommandRunner.RunGit(path, $@"config user.name ""Aiursoft""");
            await gitCommandRunner.RunGit(path, $@"commit -m ""{message}"" --author ""Aiursoft <nobody@domain.com>""");
        }
    }

    public async Task<string> GetCurrentUserEmail(string path)
    {
        try
        {
            var email = await gitCommandRunner.RunGit(path, "config user.email");
            return email.Trim();
        }
        catch (GitCommandException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Clone a repo.
    /// </summary>
    /// <param name="path">Path on disk.</param>
    /// <param name="branch">Init branch.</param>
    /// <param name="endPoint">Endpoint. Used for Git clone.</param>
    /// <param name="cloneMode">Clone mode</param>
    /// <param name="personalAccessToken">Token for cloning private repos.</param>
    /// <returns>Task</returns>
    public async Task Clone(string path, string? branch, string endPoint, CloneMode cloneMode, string? personalAccessToken = null)
    {
        var authentication = CreateAuthenticationEnvironment(endPoint, personalAccessToken);
        var cloneEndpoint = authentication == null ? endPoint : RemoveHttpsCredentials(endPoint);

        var branchArg = string.IsNullOrWhiteSpace(branch) ? string.Empty : $"-b {branch}";
        var command = cloneMode switch
        {
            CloneMode.Full => $"clone {branchArg} {cloneEndpoint} .",
            CloneMode.OnlyCommits => $"clone --filter=tree:0 {branchArg} {cloneEndpoint} .",
            CloneMode.CommitsAndTrees => $"clone --filter=blob:none {branchArg} {cloneEndpoint} .",
            CloneMode.Depth1 => $"clone --depth=1 {branchArg} {cloneEndpoint} .",
            CloneMode.Bare => $"clone --bare {branchArg} {cloneEndpoint} .",
            CloneMode.BareWithOnlyCommits => $"clone --bare --filter=tree:0 {branchArg} {cloneEndpoint} .",
            _ => throw new NotSupportedException($"Clone mode {cloneMode} is not supported.")
        };

        await gitCommandRunner.RunGit(path, command, null, true, authentication);
    }

    private static string RemoveHttpsCredentials(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.UserInfo))
        {
            return endpoint;
        }

        return new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri.AbsoluteUri;
    }

    private static Dictionary<string, string?>? CreateAuthenticationEnvironment(string endpoint, string? credential)
    {
        if (string.IsNullOrWhiteSpace(credential) ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var cleanEndpoint = RemoveHttpsCredentials(endpoint);
        // Preserve both existing user:password and token-as-username callers.
        var basicCredential = credential.Contains(':') ? credential : credential + ":";
        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(basicCredential));
        return new Dictionary<string, string?>
        {
            ["GIT_CONFIG_COUNT"] = "5",
            // Remote URLs are multi-valued: clear cached entries before supplying the current URL.
            ["GIT_CONFIG_KEY_0"] = "remote.origin.url",
            ["GIT_CONFIG_VALUE_0"] = string.Empty,
            ["GIT_CONFIG_KEY_1"] = "remote.origin.url",
            ["GIT_CONFIG_VALUE_1"] = cleanEndpoint,
            ["GIT_CONFIG_KEY_2"] = "credential.helper",
            ["GIT_CONFIG_VALUE_2"] = string.Empty,
            ["GIT_CONFIG_KEY_3"] = $"http.{cleanEndpoint}.extraHeader",
            ["GIT_CONFIG_VALUE_3"] = $"Authorization: Basic {basicAuth}",
            ["GIT_CONFIG_KEY_4"] = $"http.{cleanEndpoint}.followRedirects",
            ["GIT_CONFIG_VALUE_4"] = "false"
        };
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public async Task<bool> IsBareRepo(string path)
    {
        var gitConfigOutput = await gitCommandRunner.RunGit(path, "config --get core.bare");
        return gitConfigOutput.Contains("true");
    }

    /// <summary>
    ///     Switch a folder to a target branch (The last state of the remote).
    ///     Supports empty folder. We will clone the repo there.
    ///     Supports folder with existing content. We will clean that folder and checkout to the target branch.
    /// </summary>
    /// <param name="path">Path</param>
    /// <param name="branch">Branch name</param>
    /// <param name="endPoint">Git clone endpoint.</param>
    /// <param name="cloneMode">Clone mode</param>
    /// <param name="personalAccessToken">Optional PAT for cloning private repos.</param>
    /// <returns>Task</returns>
    public async Task ResetRepo(string path, string? branch, string endPoint, CloneMode cloneMode, string? personalAccessToken = null)
    {
        var authentication = CreateAuthenticationEnvironment(endPoint, personalAccessToken);
        var expectedEndpoint = RemoveHttpsCredentials(endPoint);
        try
        {
            var remoteWithoutAuth = RemoveHttpsCredentials(await GetRemoteUrl(path, "origin"));

            if (!string.Equals(remoteWithoutAuth, expectedEndpoint, StringComparison.OrdinalIgnoreCase))
            {
                throw new GitCommandException(
                    $"The repository with remote: '{remoteWithoutAuth}' is not a repository for {expectedEndpoint}.", "remote -v",
                    output: remoteWithoutAuth,
                    error: remoteWithoutAuth,
                    path);
            }

            if (await IsBareRepo(path))
            {
                logger.LogTrace("The repo at {Path} is a bare repo. We will fetch it.", path);
                var currentBranch = await GetBranch(path);
                await gitCommandRunner.RunGit(path, $"fetch origin {currentBranch}:{currentBranch}", null, true, authentication);
            }
            else
            {
                logger.LogTrace("The repo at {Path} is a normal repo. We will reset it.", path);
                await gitCommandRunner.RunGit(path, "reset --hard HEAD");
                await gitCommandRunner.RunGit(path, "clean . -fdx");
                if (!string.IsNullOrWhiteSpace(branch))
                {
                    logger.LogInformation("Switching to branch {Branch} at {Path}", branch, path);
                    await SwitchToBranch(path, branch, false);
                }

                await Fetch(path, null, authentication);
                if (!string.IsNullOrWhiteSpace(branch))
                {
                    await gitCommandRunner.RunGit(path, $"reset --hard origin/{branch}");
                }
                else
                {
                    await gitCommandRunner.RunGit(path, $"reset --hard origin/HEAD");
                }
            }
        }
        catch (GitCommandException e) when (
            e.Message.Contains("not a git repository") ||
            e.Message.Contains("unknown revision or path") ||
            e.Message.Contains($"is not a repository for {expectedEndpoint}"))
        {
            logger.LogInformation("The repo at {Path} is not a git repo because {Message}. We will clone it.", path,
                e.Message);
            FolderDeleter.DeleteByForce(path, true);
            await Clone(path, branch, endPoint, cloneMode, personalAccessToken);
        }
    }
    /// <summary>
    ///     Do a commit. (With adding local changes)
    /// </summary>
    /// <param name="sourcePath">Commit path.</param>
    /// <param name="message">Commie message.</param>
    /// <param name="branch">Branch</param>
    /// <returns>Saved.</returns>
    public async Task<bool> CommitToBranch(string sourcePath, string message, string branch)
    {
        await gitCommandRunner.RunGit(sourcePath, "add .");
        await SwitchToBranch(sourcePath, branch, true);
        var commitResult = await gitCommandRunner.RunGit(sourcePath, $@"commit -m ""{message}""");
        return !commitResult.Contains("nothing to commit, working tree clean");
    }

    // ReSharper disable once UnusedMember.Global
    public async Task SetUserConfig(string sourcePath, string username, string email)
    {
        await gitCommandRunner.RunGit(sourcePath, $"""
                                                   config user.name "{username.Replace("\"", "\\\"")}"
                                                   """);
        await gitCommandRunner.RunGit(sourcePath, $"""
                                                   config user.email "{email.Replace("\"", "\\\"")}"
                                                   """);
    }

    /// <summary>
    ///     Push a local folder to remote.
    /// </summary>
    /// <param name="sourcePath">Folder path..</param>
    /// <param name="branch">Remote branch.</param>
    /// <param name="endpoint">Endpoint</param>
    /// <param name="force">Force</param>
    /// <returns>Pushed.</returns>
    public async Task Push(string sourcePath, string branch, string endpoint, bool force = false)
    {
        // Set origin url.
        try
        {
            await gitCommandRunner.RunGit(sourcePath, $@"remote set-url ninja {endpoint}");
        }
        catch (GitCommandException e) when (e.GitError.Contains("No such remote"))
        {
            await gitCommandRunner.RunGit(sourcePath, $@"remote add ninja {endpoint}");
        }

        // Push to that origin.
        try
        {
            var forceString = force ? "--force" : string.Empty;

            var command = $@"push --set-upstream ninja {branch} {forceString}";
            logger.LogInformation("Running git {Command}", command);
            await gitCommandRunner.RunGit(sourcePath, command);
        }
        catch (GitCommandException e) when (e.GitError.Contains("rejected]"))
        {
            // In this case, the remote branch is later than local.
            // So we might have some conflict.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Git push failed to {SourcePath}, branch {Branch}, endpoint {Endpoint}", sourcePath,
                branch, endpoint);
            throw;
        }
    }

    /// <summary>
    ///     If current path is pending a git commit.
    /// </summary>
    /// <param name="sourcePath">Path</param>
    /// <returns>Bool</returns>
    public async Task<bool> PendingCommit(string sourcePath)
    {
        var statusResult = await gitCommandRunner.RunGit(sourcePath, @"status");
        var clean = statusResult.Contains("working tree clean");
        return !clean;
    }

    /// <summary>
    /// Fetches updates from the remote repository for the specified Git repository path.
    /// It includes a retry mechanism to handle potential failures.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <returns>A task representing the asynchronous fetch operation.</returns>
    public Task Fetch(string path)
    {
        return Fetch(path, timeout: null);
    }

    /// <summary>
    /// Fetches updates from the remote repository for the specified Git repository path.
    /// It includes a retry mechanism to handle potential failures.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <param name="timeout">Optional timeout for each fetch attempt.</param>
    /// <returns>A task representing the asynchronous fetch operation.</returns>
    public Task Fetch(string path, TimeSpan? timeout)
    {
        return Fetch(path, timeout, null);
    }

    public Task Fetch(string path, TimeSpan? timeout, IDictionary<string, string?>? environmentVariables)
    {
        return retryEngine.RunWithRetry(
            attempt => gitCommandRunner.RunGit(
                path,
                "fetch --verbose",
                timeout ?? TimeSpan.FromSeconds(attempt * 50), true, environmentVariables));
    }

    /// <summary>
    /// Lists all branches available from the specified remote on the Git repository.
    /// Removes the remote prefix from each branch name for clarity.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <param name="remote">The name of the remote (default is "origin").</param>
    /// <returns>An array of branch names from the remote.</returns>
    public Task<string[]> ListRemoteBranches(string path, string remote = "origin")
    {
        return ListRemoteBranches(path, remote, null);
    }

    public async Task<string[]> ListRemoteBranches(string path, string remote, IDictionary<string, string?>? environmentVariables)
    {
        await Fetch(path, null, environmentVariables);
        var remoteOutput = await gitCommandRunner.RunGit(path, "branch -r", null, true, environmentVariables);
        return remoteOutput
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith($"{remote}/") && !line.Contains("->"))
            .Select(line => line.Substring(remote.Length + 1))
            .ToArray();
    }

    /// <summary>
    /// Lists all local branches in the specified Git repository.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <returns>An array of local branch names.</returns>
    public Task<string[]> GetAllLocalBranches(string path)
    {
        return GetAllLocalBranches(path, null);
    }

    public async Task<string[]> GetAllLocalBranches(string path, IDictionary<string, string?>? environmentVariables)
    {
        var localOutput = await gitCommandRunner.RunGit(path, "branch --format=\"%(refname:short)\"", null, true, environmentVariables);
        return localOutput
            .Split('\n')
            .Select(line => line.Trim(' ', '"'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    /// <summary>
    /// Deletes a local Git branch.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <param name="branchName">The name of the branch to delete.</param>
    /// <param name="force">Whether to force delete the branch (using -D).</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public Task DeleteLocalBranch(string path, string branchName, bool force = false)
    {
        return DeleteLocalBranch(path, branchName, force, null);
    }

    public async Task DeleteLocalBranch(string path, string branchName, bool force, IDictionary<string, string?>? environmentVariables)
    {
        var flag = force ? "-D" : "-d";
        logger.LogInformation("Deleting local branch {Branch} in {Path} with flag {Flag}.", branchName, path, flag);
        await gitCommandRunner.RunGit(path, $"branch {flag} {branchName}", null, true, environmentVariables);
    }

    /// <summary>
    /// Deletes all local branches that do not exist on the specified remote repository.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <param name="remote">The name of the remote (default is "origin").</param>
    /// <returns>A task representing the asynchronous deletion operation.</returns>
    public Task DeleteLocalBranchesNotInRemote(string path, string remote = "origin")
    {
        return DeleteLocalBranchesNotInRemote(path, remote, null);
    }

    public async Task DeleteLocalBranchesNotInRemote(string path, string remote, IDictionary<string, string?>? environmentVariables)
    {
        var remoteBranches = await ListRemoteBranches(path, remote, environmentVariables);
        var localBranches = (await GetAllLocalBranches(path, environmentVariables)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var branch in localBranches)
        {
            if (!remoteBranches.Contains(branch))
            {
                logger.LogInformation("Deleting local branch {Branch} in {Path} because it does not exist on the remote.",
                    branch, path);
                try
                {
                    await DeleteLocalBranch(path, branch, true, environmentVariables);
                }
                catch (GitCommandException ex)
                {
                    logger.LogWarning(ex, "Failed to delete branch {Branch} in {Path}", branch, path);
                    throw;
                }
            }
        }
    }

    public Task CreateLocalBranchFromRemote(string path, string remote = "origin")
    {
        return CreateLocalBranchFromRemote(path, remote, null);
    }

    public async Task CreateLocalBranchFromRemote(string path, string remote, IDictionary<string, string?>? environmentVariables)
    {
        var remoteBranches = await ListRemoteBranches(path, remote, environmentVariables);
        var localBranches = (await GetAllLocalBranches(path, environmentVariables)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var branch in remoteBranches)
        {
            if (!localBranches.Contains(branch))
            {
                logger.LogInformation("Creating local branch {Branch} in {Path} because it does not exist locally.",
                    branch, path);
                try
                {
                    await gitCommandRunner.RunGit(path, $"checkout -b {branch}", null, true, environmentVariables);
                    await gitCommandRunner.RunGit(path, $"reset --hard {remote}/{branch}", null, true, environmentVariables);
                    // Set the upstream branch to the remote branch
                    await gitCommandRunner.RunGit(path, $"branch --set-upstream-to={remote}/{branch} {branch}", null, true, environmentVariables);
                }
                catch (GitCommandException ex)
                {
                    logger.LogWarning(ex, "Failed to create branch {Branch} in {Path}", branch, path);
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Mirrors all local branches to their corresponding remote branches by resetting to the remote state.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <param name="remote">The name of the remote (default is "origin").</param>
    /// <returns>A task representing the asynchronous mirroring operation.</returns>
    public Task EnsureAllLocalBranchesUpToDateWithRemote(string path, string remote = "origin")
    {
        return EnsureAllLocalBranchesUpToDateWithRemote(path, remote, null);
    }

    public async Task EnsureAllLocalBranchesUpToDateWithRemote(string path, string remote, IDictionary<string, string?>? environmentVariables)
    {
        await DeleteLocalBranchesNotInRemote(path, remote, environmentVariables);
        await CreateLocalBranchFromRemote(path, remote, environmentVariables);

        var localBranches = await GetAllLocalBranches(path, environmentVariables);
        foreach (var branch in localBranches)
        {
            try
            {
                await gitCommandRunner.RunGit(path, $"checkout {branch}", null, true, environmentVariables);
                await gitCommandRunner.RunGit(path, $"reset --hard {remote}/{branch}", null, true, environmentVariables);
                // We don't have to run `git pull` here because we already fetched the remote branches.
            }
            catch (GitCommandException ex)
            {
                logger.LogWarning(ex, "Failed to mirror branch {Branch} in {Path}", branch, path);
                throw;
            }
        }
    }

    /// <summary>
    /// Pushes all local branches and tags to the specified remote repository, with optional forced updates.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <param name="remoteName">The name of the remote.</param>
    /// <param name="force">Whether to force push changes (default is false).</param>
    /// <returns>A task representing the asynchronous push operation.</returns>
    public async Task PushAllBranchesAndTags(string path, string remoteName, bool force = false)
    {
        var forceArg = force ? "--force" : string.Empty;
        await gitCommandRunner.RunGit(path, $"push {remoteName} --all {forceArg}");
        await gitCommandRunner.RunGit(path, $"push {remoteName} --tags {forceArg}");
    }

    public async Task<string[]> GetRemoteNames(string path)
    {
        var remoteOutput = await gitCommandRunner.RunGit(path, "remote");
        return remoteOutput
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    public async Task AddOrSetRemoteUrl(string path, string remoteName, string remoteUrl)
    {
        var remotes = await GetRemoteNames(path);
        if (remotes.Contains(remoteName))
        {
            var currentRemoteUrl = await GetRemoteUrl(path, remoteName);
            if (string.Equals(currentRemoteUrl, remoteUrl, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Remote {RemoteName} already exists with URL {RemoteUrl} in {Path}, skipping.",
                    remoteName, remoteUrl, path);
                return;
            }

            await SetRemoteUrl(path, remoteName, remoteUrl);
        }
        else
        {
            await AddRemote(path, remoteName, remoteUrl);
        }
    }

    public async Task AddRemote(string path, string remoteName, string remoteUrl)
    {
        var existingRemotes = await GetRemoteNames(path);
        if (existingRemotes.Contains(remoteName))
        {
            throw new GitCommandException(
                message: $"Remote {remoteName} already exists in {path}.",
                command: $"remote -v",
                output: string.Empty,
                error: string.Empty,
                path);
        }

        logger.LogInformation("Adding remote {RemoteName} with URL {RemoteUrl} in {Path}.", remoteName, remoteUrl, path);
        await gitCommandRunner.RunGit(path, $"remote add {remoteName} {remoteUrl}");
    }

    public async Task DeleteRemote(string path, string remoteName)
    {
        var existingRemotes = await GetRemoteNames(path);
        if (!existingRemotes.Contains(remoteName))
        {
            throw new GitCommandException(
                message: $"Remote {remoteName} does not exist in {path}.",
                command: $"remote -v",
                output: string.Empty,
                error: string.Empty,
                path);
        }

        logger.LogInformation("Deleting remote {RemoteName} in {Path}.", remoteName, path);
        await gitCommandRunner.RunGit(path, $"remote remove {remoteName}");
    }

    public async Task<string> GetRemoteUrl(string path, string remoteName)
    {
        var remoteOutput = await gitCommandRunner.RunGit(path, $"remote get-url {remoteName}");
        return remoteOutput.Trim();
    }

    /// <summary>
    /// Sets the URL for the specified remote. Adds the remote if it does not exist.
    /// </summary>
    /// <param name="path">The path of the Git repository.</param>
    /// <param name="remoteName">The name of the remote.</param>
    /// <param name="remoteUrl">The URL of the remote.</param>
    /// <returns>A task representing the asynchronous operation to set or add the remote URL.</returns>
    public async Task SetRemoteUrl(string path, string remoteName, string remoteUrl)
    {
        var localRemoteUrl = await GetRemoteNames(path);
        if (!localRemoteUrl.Contains(remoteName))
        {
            throw new GitCommandException(
                message: $"Remote {remoteName} does not exist in {path}.",
                command: $"remote -v",
                output: string.Empty,
                error: string.Empty,
                path);
        }

        logger.LogInformation("Setting remote {RemoteName} with URL {RemoteUrl} in {Path}.", remoteName, remoteUrl, path);
        await gitCommandRunner.RunGit(path, $"remote set-url {remoteName} {remoteUrl}");
    }
}
