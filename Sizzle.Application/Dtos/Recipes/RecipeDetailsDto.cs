namespace Sizzle.Application.Dtos.Recipes;

public class RecipeDetailsDto : FeedRecipeDto
{
    public IReadOnlyList<RecipeStepDto> Steps { get; init; } = [];

    public IReadOnlyList<RecipeIngredientDto> Ingredients { get; init; } = [];
}