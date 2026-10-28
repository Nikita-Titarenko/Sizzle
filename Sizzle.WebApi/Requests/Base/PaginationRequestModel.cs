using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Base;

public class PaginationRequestModel
{
    [Range(0, int.MaxValue)]
    public int PageIndex { get; init; }

    [Range(1, int.MaxValue)]
    public int PageSize { get; init; } = 10;
}