namespace Sizzle.Application.Dtos.Recipes;

public class RecipeIngredientDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public string Unit { get; init; } = string.Empty;
}