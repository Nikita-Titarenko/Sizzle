namespace Sizzle.Application.Dtos.Recipes;

public class GetRecipesDto
{
    public Guid[] IngredientIds { get; init; } = [];

    public Guid[] CategoryIds { get; init; } = [];

    public string? Search { get; init; }

    public int PageIndex { get; init; }

    public int PageSize { get; init; }

    public Guid? ApplicationUserId { get; init; }

    public Guid? OwnerId { get; init; }
}