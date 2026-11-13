namespace Sizzle.Application.Dtos.Ingredients;

public class IngredientListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public int Calories { get; init; }

    public List<IngredientListItemDto> Children { get; init; } = [];
}
