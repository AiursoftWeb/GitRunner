namespace Aiursoft.GitRunner.Exceptions;


/// <summary>
///     A git command exception.
/// </summary>
public class GitCommandException : Exception
{
    /// <summary>
    ///     Creates new GitCommandException
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="command">Command tried to run</param>
    /// <param name="output">Output.</param>
    /// <param name="error">Result.</param>
    /// <param name="path">Path.</param>
    public GitCommandException(
        string message,
        string command,
        string output,
        string error,
        string path)
        : base(GitDiagnostics.Redact(message))
    {
        Command = GitDiagnostics.Redact(command);
        GitOutput = GitDiagnostics.Redact(output);
        GitError = GitDiagnostics.Redact(error);
        Path = GitDiagnostics.Redact(path);
    }

    /// <summary>
    ///     Command tried to run.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Output.
    /// </summary>
    public string GitOutput { get; }
    
    /// <summary>
    /// Error.
    /// </summary>
    public string GitError { get; }

    /// <summary>
    ///     Executing path.
    /// </summary>
    public string Path { get; }
}
