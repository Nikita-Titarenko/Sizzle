namespace Sizzle.Domain.Common;

public class Result
{
    public List<Error> Errors { get; init; } = [];

    public bool IsSuccess => Errors.Count == 0;
}
