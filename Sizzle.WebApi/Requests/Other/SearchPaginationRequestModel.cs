namespace Sizzle.WebApi.Requests.Other;

public class SearchPaginationRequestModel
{
    public string? Search { get; init; }

    public int? PageIndex { get; init; }

    public int? PageSize { get; init; }
}
