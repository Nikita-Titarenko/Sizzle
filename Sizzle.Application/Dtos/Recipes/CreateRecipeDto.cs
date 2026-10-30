namespace Sizzle.Application.Dtos.Recipes;

public class CreateRecipeDto
{
    public required Guid ApplicationUserId { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required int CookingTimeMinutes { get; init; }

    public required Guid CategoryId { get; init; }

    public RecipeMediaUploadDto? File { get; init; }

    public RecipeStepInputDto[] Steps { get; init; } = [];

    public RecipeIngredientInputDto[] Ingredients { get; init; } = [];

    public NewIngredientDto[] NewIngredients { get; init; } = [];
}
