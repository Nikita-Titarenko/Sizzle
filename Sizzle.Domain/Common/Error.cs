namespace Sizzle.Domain.Common;

public class Error
{
    public required string Message { get; init; }

    public string Field { get; init; } = string.Empty;

    public ErrorKey Key { get; init; }

    public Dictionary<string, string> Parameters { get; init; } = [];
}
