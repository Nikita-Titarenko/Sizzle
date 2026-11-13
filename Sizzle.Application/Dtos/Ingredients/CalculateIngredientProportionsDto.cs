namespace Sizzle.Application.Dtos.Ingredients;

public class CalculateIngredientProportionsDto
{
    public IEnumerable<IngredientProportionItemDto> Initial { get; init; } = [];

    public IngredientProportionItemDto ChangedIngredient { get; init; } = new();
}