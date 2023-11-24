﻿using Aiursoft.Canon;
using Aiursoft.GitRunner.Exceptions;
using Aiursoft.GitRunner.Models;
using Aiursoft.GitRunner.Services;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aiursoft.GitRunner;

/// <summary>
///     Workspace initializer.
/// </summary>
public class WorkspaceManager : ITransientDependency
{
    private readonly CommandRunner _commandRunner;
    private readonly ILogger<WorkspaceManager> _logger;
    private readonly RetryEngine _retryEngine;

    public WorkspaceManager(
        ILogger<WorkspaceManager> logger,
        RetryEngine retryEngine,
        CommandRunner commandRunner)
    {
        _logger = logger;
        _retryEngine = retryEngine;
        _commandRunner = commandRunner;
    }

    /// <summary>
    ///     Get current branch from a git repo.
    /// </summary>
    /// <param name="path">Path</param>
    /// <returns>Current branch.</returns>
    public async Task<string> GetBranch(string path)
    {
        var gitBranchOutput = await _commandRunner.RunGit(path, "rev-parse --abbrev-ref HEAD");
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
        var times = new List<DateTime>();
        var gitCommitsOutput = await _commandRunner.RunGit(path, "--no-pager log --format=%at");
        var lines = gitCommitsOutput.Split('\n');
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

    public async Task SwitchToBranch(string sourcePath, string targetBranch, bool fromCurrent)
    {
        var currentBranch = await GetBranch(sourcePath);
        if (string.Equals(currentBranch, targetBranch, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            await _commandRunner.RunGit(sourcePath, $"checkout -b {targetBranch}");
        }
        catch (GitCommandException e) when (e.Message.Contains("already exists"))
        {
            if (fromCurrent)
            {
                await _commandRunner.RunGit(sourcePath, $"branch -D {targetBranch}");
                await SwitchToBranch(sourcePath, targetBranch, fromCurrent);
            }
            else
            {
                await _commandRunner.RunGit(sourcePath, $"checkout {targetBranch}");
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
        var gitRemoteOutput = await _commandRunner.RunGit(path, "remote -v");
        return gitRemoteOutput
            .Split('\n')
            .First(t => t.StartsWith("origin"))
            .Substring(6)
            .Replace("(fetch)", string.Empty)
            .Replace("(push)", string.Empty)
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
        string? command;
        var branchArg = string.IsNullOrWhiteSpace(branch) ? string.Empty : $"-b {branch}";
        command = cloneMode switch
        {
            CloneMode.Full => $"clone {branchArg} {endPoint} .",
            CloneMode.OnlyCommits => $"clone --filter=tree:0 {branchArg} {endPoint} .",
            CloneMode.CommitsAndTrees => $"clone --filter=blob:none {branchArg} {endPoint} .",
            CloneMode.Depth1 => $"clone --depth=1 {branchArg} {endPoint} .",
            _ => throw new NotSupportedException($"Clone mode {cloneMode} is not supported.")
        };

        await _commandRunner.RunGit(path, command);
    }

    /// <summary>
    ///     Switch a folder to a target branch (Latest remote).
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
                throw new GitCommandException(
                    $"The repository with remote: '{remote}' is not a repository for {endPoint}.", "remote -v", remote,
                    path);

            await _commandRunner.RunGit(path, "reset --hard HEAD");
            await _commandRunner.RunGit(path, "clean . -fdx");
            if (!string.IsNullOrWhiteSpace(branch))
            {
                await SwitchToBranch(path, branch, false);
            }
            await Fetch(path);
            if (!string.IsNullOrWhiteSpace(branch))
            {
                await _commandRunner.RunGit(path, $"reset --hard origin/{branch}");
            }
            else
            {
                await _commandRunner.RunGit(path, $"reset --hard origin/HEAD");                
            }
        }
        catch (GitCommandException e) when (
            e.Message.Contains("not a git repository") ||
            e.Message.Contains("unknown revision or path") ||
            e.Message.Contains($"is not a repository for {endPoint}"))
        {
            ClearPath(path);
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
        await _commandRunner.RunGit(sourcePath, "add .");
        await SwitchToBranch(sourcePath, branch, true);
        var commitResult = await _commandRunner.RunGit(sourcePath, $@"commit -m ""{message}""");
        return !commitResult.Contains("nothing to commit, working tree clean");
    }

    public async Task SetUserConfig(string sourcePath, string username, string email)
    {
        await _commandRunner.RunGit(sourcePath, $@"config user.name ""{username}""");
        await _commandRunner.RunGit(sourcePath, $@"config user.email ""{email}""");
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
            await _commandRunner.RunGit(sourcePath, $@"remote set-url ninja {endpoint}");
        }
        catch (GitCommandException e) when (e.GitOutput.Contains("No such remote"))
        {
            await _commandRunner.RunGit(sourcePath, $@"remote add ninja {endpoint}");
        }

        // Push to that origin.
        try
        {
            var forceString = force ? "--force" : string.Empty;

            var command = $@"push --set-upstream ninja {branch} {forceString}";
            _logger.LogInformation("Running git {Command}", command);
            await _commandRunner.RunGit(sourcePath, command);
        }
        catch (GitCommandException e) when (e.GitOutput.Contains("rejected]"))
        {
            // In this case, the remote branch is later than local.
            // So we might have some conflict.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git push failed to {SourcePath}, branch {Branch}, endpoint {Endpoint}", sourcePath, branch, endpoint);
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
        var statusResult = await _commandRunner.RunGit(sourcePath, @"status");
        var clean = statusResult.Contains("working tree clean");
        return !clean;
    }

    public Task Fetch(string path)
    {
        return _retryEngine.RunWithRetry(
            async attempt =>
            {
                var workJob = _commandRunner.RunGit(path, "fetch --verbose",
                    attempt % 2 == 0);
                var waitJob = Task.Delay(TimeSpan.FromSeconds(attempt * 50));
                await Task.WhenAny(workJob, waitJob);
                if (workJob.IsCompleted)
                    return await workJob;
                throw new TimeoutException("Git fetch job has exceeded the timeout and we have to retry it.");
            });
    }

    public void ClearPath(string path)
    {
        var di = new DirectoryInfo(path);
        foreach (var file in di.GetFiles()) file.Delete();

        foreach (var dir in di.GetDirectories()) dir.Delete(true);
    }
}