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

    /// <summary>
    ///     Get remote origin's URL from a local git repo.
    /// </summary>
    /// <param name="path">Path.</param>
    /// <returns>Remote URL.</returns>
    public async Task<string> GetRemoteUrl(string path)
    {
        var gitRemoteOutput = await gitCommandRunner.RunGit(path, "remote -v");
        
        if (string.IsNullOrWhiteSpace(gitRemoteOutput))
        {
            return string.Empty;
        }
        
        return gitRemoteOutput
            .Split('\n')
            .First(t => t.StartsWith("origin"))
            .Substring(6)
            .Split(' ')
            .First()
            .Trim();
    }

    /// <summary>
    ///     Clone a repo.
    /// </summary>
    /// <param name="path">Path on disk.</param>
    /// <param name="branch">Init branch.</param>
    /// <param name="endPoint">Endpoint. Used for Git clone.</param>
    /// <param name="cloneMode">Clone mode</param>
    /// <returns>Task</returns>
    public async Task Clone(string path, string? branch, string endPoint, CloneMode cloneMode)
    {
        var branchArg = string.IsNullOrWhiteSpace(branch) ? string.Empty : $"-b {branch}";
        var command = cloneMode switch
        {
            CloneMode.Full => $"clone {branchArg} {endPoint} .",
            CloneMode.OnlyCommits => $"clone --filter=tree:0 {branchArg} {endPoint} .",
            CloneMode.CommitsAndTrees => $"clone --filter=blob:none {branchArg} {endPoint} .",
            CloneMode.Depth1 => $"clone --depth=1 {branchArg} {endPoint} .",
            CloneMode.Bare => $"clone --bare {branchArg} {endPoint} .",
            CloneMode.BareWithOnlyCommits => $"clone --bare --filter=tree:0 {branchArg} {endPoint} .",
            _ => throw new NotSupportedException($"Clone mode {cloneMode} is not supported.")
        };

        await gitCommandRunner.RunGit(path, command);
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
    /// <returns>Task</returns>
    public async Task ResetRepo(string path, string? branch, string endPoint, CloneMode cloneMode)
    {
        try
        {
            var remote = await GetRemoteUrl(path);
            if (!string.Equals(remote, endPoint, StringComparison.OrdinalIgnoreCase))
            {
                throw new GitCommandException(
                    $"The repository with remote: '{remote}' is not a repository for {endPoint}.", "remote -v", 
                    output: remote,
                    error: remote,
                    path);
            }

            if (await IsBareRepo(path))
            {
                logger.LogTrace("The repo at {Path} is a bare repo. We will fetch it.", path);
                var currentBranch = await GetBranch(path);
                await gitCommandRunner.RunGit(path, $"fetch origin {currentBranch}:{currentBranch}");
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

                await Fetch(path);
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
            e.Message.Contains($"is not a repository for {endPoint}"))
        {
            logger.LogInformation("The repo at {Path} is not a git repo because {Message}. We will clone it.", path, e.Message);
            FolderDeleter.DeleteByForce(path, true);
            await Clone(path, branch, endPoint, cloneMode);
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
            logger.LogWarning(ex, "Git push failed to {SourcePath}, branch {Branch}, endpoint {Endpoint}", sourcePath, branch, endpoint);
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

    public Task Fetch(string path)
    {
        return retryEngine.RunWithRetry(
            async attempt =>
            {
                var workJob = gitCommandRunner.RunGit(path, "fetch --verbose");
                var waitJob = Task.Delay(TimeSpan.FromSeconds(attempt * 50));
                await Task.WhenAny(workJob, waitJob);
                if (workJob.IsCompleted)
                    return await workJob;
                throw new TimeoutException("Git fetch job has exceeded the timeout and we have to retry it.");
            });
    }
}