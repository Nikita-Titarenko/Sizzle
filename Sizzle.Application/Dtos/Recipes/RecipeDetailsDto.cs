namespace Sizzle.Application.Dtos.Recipes;

public class RecipeDetailsDto : RecipeDto
{
    public IReadOnlyList<RecipeStepDto> Steps { get; init; } = [];

    public IReadOnlyList<RecipeIngredientDto> Ingredients { get; init; } = [];
}

public class RecipeStepDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> FileUrls { get; init; } = [];
}

public class RecipeIngredientDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;
}