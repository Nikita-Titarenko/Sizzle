using Sizzle.Application.Dtos.Base;

namespace Sizzle.Application.Dtos.Recipes;

public class GetRecipesDto : PaginationDto
{
    public RecipeIngredientFilterDto[] Ingredients { get; init; } = [];

    public Guid[] CategoryIds { get; init; } = [];

    public string? Search { get; init; }

    public Guid? ApplicationUserId { get; init; }

    public Guid? OwnerId { get; init; }
}