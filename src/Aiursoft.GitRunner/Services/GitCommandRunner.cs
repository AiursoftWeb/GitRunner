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
    /// <param name="setSafeDirectoryFirst">setSafeDirectoryFirst</param>
    /// <returns>Task</returns>
    public Task<string> RunGit(string path, string arguments, TimeSpan? timeout = null, bool setSafeDirectoryFirst = true)
    {
        return RunGit(path, arguments, timeout, setSafeDirectoryFirst, null);
    }

    public async Task<string> RunGit(string path, string arguments, TimeSpan? timeout,
        bool setSafeDirectoryFirst, IDictionary<string, string?>? environmentVariables)
    {
        // Copy per-command settings so adding safe.directory never mutates the caller's credentials.
        var commandEnvironment = environmentVariables == null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?>(environmentVariables);
        var commandArguments = arguments;
        if (setSafeDirectoryFirst)
        {
            // Scope trust to this command without writing the shared global config.
            commandEnvironment["AIURSOFT_GITRUNNER_SAFE_DIRECTORY"] = Path.GetFullPath(path);
            commandArguments = "--config-env=safe.directory=AIURSOFT_GITRUNNER_SAFE_DIRECTORY " + arguments;
        }
        
        string output;
        string error;
        try
        {
            _logger.LogTrace("Running git command {Command} at {Path}", GitDiagnostics.Redact(arguments), path);
            (var exitCode, output, error) = await _commandService.RunCommandAsync(
                bin: "git", 
                arg:commandArguments,
                path: path,
                timeout: timeout,
                environmentVariables: commandEnvironment);
            
            if (
                output.Contains("'git-lfs' was not found") ||
                error.Contains("'git-lfs' was not found") ||
                output.Contains("git-lfs: command not found") ||
                error.Contains("git-lfs: command not found"))
            {
                throw new GitCommandException(
                    "Start Git failed! Git LFS not found!",
                    arguments,
                    string.Empty,
                    "Start git failed.",
                    path);
            }
            
            if (exitCode != 0)
            {
                throw new GitCommandException(
                    $"Git command resulted an error: git {arguments} on {path} got output: {output}, error: {error}",
                    arguments,
                    output,
                    error,
                    path);
            }
        }
        catch (TimeoutException exception)
        {
            // The process runner includes arguments in timeout messages; do not retain a raw inner exception.
            throw new TimeoutException(GitDiagnostics.Redact(exception.Message));
        }
        catch (Win32Exception)
        {
            throw new GitCommandException(
                "Start Git failed! Git not found!",
                arguments,
                string.Empty,
                "Start git failed.",
                path);
        }

        var finalOutput = output + error;
        _logger.LogTrace("Console command {Command} provided following success output {Output}",
            GitDiagnostics.Redact(arguments), GitDiagnostics.Redact(finalOutput));
        return finalOutput;
    }
}
