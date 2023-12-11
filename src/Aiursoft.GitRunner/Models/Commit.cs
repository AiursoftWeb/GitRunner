namespace Aiursoft.GitRunner.Models;

public class Commit
{
    public string Author { get; init; } = string.Empty;
    
    public string Email { get; init; } = string.Empty;
    
    public string Message { get; init; } = string.Empty;

    public DateTime Time { get; init; }
    
    public string Hash { get; init; } = string.Empty;
}