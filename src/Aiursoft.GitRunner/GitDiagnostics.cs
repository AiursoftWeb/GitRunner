using System.Text.RegularExpressions;

namespace Aiursoft.GitRunner;

internal static class GitDiagnostics
{
    public static string Redact(string value)
    {
        return Regex.Replace(value, @"([a-z][a-z0-9+.-]*://)[^\s/""'<>]+@", "$1[REDACTED]@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    }
}
