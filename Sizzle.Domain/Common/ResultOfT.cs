namespace Sizzle.Domain.Common;

public class Result<T> : Result
{
    public T? Value { get; init; }
}