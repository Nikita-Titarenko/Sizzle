using Sizzle.Application.Dtos.Base;

namespace Sizzle.Application.Dtos.Ingredients;

public class GetAllIngredientsDto : PaginationDto
{
    public string? Search { get; init; }

    public string LanguageCode { get; init; } = "en";
}
