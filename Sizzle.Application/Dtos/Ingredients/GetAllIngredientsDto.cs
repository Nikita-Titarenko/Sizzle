namespace Sizzle.Application.Dtos.Ingredients;

public class GetAllIngredientsDto
{
    public string? Search { get; init; }

    public int? PageIndex { get; init; }

    public int? PageSize { get; init; }
}
