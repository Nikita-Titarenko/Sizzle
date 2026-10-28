namespace Sizzle.Application.Dtos.Recipes;

public class RecipeStepInputDto
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public RecipeMediaUploadDto[] Files { get; init; } = [];
}