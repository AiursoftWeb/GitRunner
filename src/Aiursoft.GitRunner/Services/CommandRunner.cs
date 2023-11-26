using Aiursoft.GitRunner.Exceptions;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Aiursoft.GitRunner.Services;

public class CommandRunner : ITransientDependency
{
    private readonly ILogger<CommandRunner> _logger;

    public CommandRunner(ILogger<CommandRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Run git command.
    /// </summary>
    /// <param name="path">Path</param>
    /// <param name="arguments">Arguments</param>
    /// <param name="integrateResultInProcess">integrateResultInProcess</param>
    /// <param name="timeout">timeout</param>
    /// <returns>Task</returns>
    public async Task<string> RunGit(string path, string arguments, bool integrateResultInProcess = true, TimeSpan? timeout = null)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        timeout ??= TimeSpan.FromMinutes(2);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WindowStyle = integrateResultInProcess ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Minimized,
                UseShellExecute = false,
                CreateNoWindow = integrateResultInProcess,
                RedirectStandardOutput = integrateResultInProcess,
                RedirectStandardError = integrateResultInProcess,
                WorkingDirectory = path
            }
        };

        _logger.LogTrace("Running command: {Trim} git {Arguments}", path.TrimEnd('\\').Trim(), arguments);

        try
        {
            process.Start();
        }
        catch
        {
            throw new GitCommandException(
                "Start Git failed! Please install Git at https://git-scm.com .",
                arguments,
                "Start git failed.",
                path);
        }


        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var readOutputTask = Task.Run(async () =>
        {
            while (!process.StandardOutput.EndOfStream)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                outputBuilder.AppendLine(line);
            }
        });
        var readErrorTask = Task.Run(async () =>
        {
            while (!process.StandardError.EndOfStream)
            {
                var line = await process.StandardError.ReadLineAsync();
                errorBuilder.AppendLine(line);
            }
        });

        var executeTask = process.WaitForExitAsync();
        var programTask = Task.WhenAll(readOutputTask, readErrorTask, executeTask);
        await Task.WhenAny(Task.Delay(timeout.Value), programTask);
        if (!programTask.IsCompleted)
        {
            throw new TimeoutException($@"Execute git command: git {arguments} at {path} was time out! Timeout is {timeout}.");
        }

        if (!integrateResultInProcess) return string.Empty;

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
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
