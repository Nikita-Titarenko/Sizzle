namespace Sizzle.Application.Dtos;

public class PageResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];

    public int TotalSize { get; init; }

    public int PageIndex { get; init; }

    public int PageSize { get; init; }
}