namespace Sizzle.Application.Dtos.Recipes;

public class RecipeStepDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> FileUrls { get; init; } = [];
}