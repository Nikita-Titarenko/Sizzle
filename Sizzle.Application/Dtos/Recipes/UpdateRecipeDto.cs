namespace Sizzle.Application.Dtos.Recipes;

public class UpdateRecipeDto
{
    public required Guid Id { get; init; }

    public required Guid ApplicationUserId { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public RecipeMediaUploadDto? File { get; init; }

    public RecipeStepInputDto[] Steps { get; init; } = [];

    public Guid[] IngredientIds { get; init; } = [];

    public string[] NewIngredientNames { get; init; } = [];
}
