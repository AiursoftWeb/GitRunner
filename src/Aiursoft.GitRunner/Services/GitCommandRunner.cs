using System.ComponentModel;
using Aiursoft.GitRunner.Exceptions;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Logging;
using Aiursoft.CSTools.Services;

namespace Aiursoft.GitRunner.Services;

public class GitCommandRunner : ITransientDependency
{
    private readonly CommandService _commandService;
    private readonly ILogger<GitCommandRunner> _logger;

    public GitCommandRunner(
        CommandService commandService,
        ILogger<GitCommandRunner> logger)
    {
        _commandService = commandService;
        _logger = logger;
    }

    /// <summary>
    ///     Run git command.
    /// </summary>
    /// <param name="path">Path</param>
    /// <param name="arguments">Arguments</param>
    /// <param name="timeout">timeout</param>
    /// <returns>Task</returns>
    public async Task<string> RunGit(string path, string arguments, TimeSpan? timeout = null)
    {
        string output;
        string error;
        try
        {
            _logger.LogTrace("Running git command {Command} at {Path}", arguments, path);
            (_, output, error) = await _commandService.RunCommandAsync(
                bin: "git", 
                arg:arguments,
                path: path,
                timeout: timeout);
        }
        catch (Win32Exception)
        {
            throw new GitCommandException(
                "Start Git failed! Git not found!",
                arguments,
                "Start git failed.",
                path);
        }
        if (
            output.Contains("'git-lfs' was not found") ||
            error.Contains("'git-lfs' was not found") ||
            output.Contains("git-lfs: command not found") ||
            error.Contains("git-lfs: command not found"))
        {
            throw new GitCommandException(
                "Start Git failed! Git LFS not found!",
                arguments,
                "Start git failed.",
                path);
        }

        var finalOutput = string.Empty;

        if (!string.IsNullOrWhiteSpace(error))
        {
            finalOutput = error;
            if (error.Contains("fatal") || error.Contains("error:"))
            {
                _logger.LogTrace("Console command {Command} provided following fatal output {Output}", arguments, finalOutput);
                throw new GitCommandException(
                    $"Git command resulted an error: git {arguments} on {path} got result: {error}",
                    arguments,
                    error,
                    path);
            }
        }

        finalOutput += output;
        _logger.LogTrace("Console command {Command} provided following success output {Output}", arguments, finalOutput);
        return finalOutput;
    }
}
