using Sizzle.Domain.Entities;

namespace Sizzle.Application.Dtos.Recipes;

public sealed class ApprovedNewIngredientDto(string name, Guid? parentId, IngredientUnit unit, int quantity, int calories,
    IReadOnlyDictionary<string, string> translations)
{
    public string Name { get; } = name;

    public Guid? ParentId { get; } = parentId;

    public IngredientUnit Unit { get; } = unit;

    public int Quantity { get; } = quantity;

    public int Calories { get; } = calories;

    public IReadOnlyDictionary<string, string> Translations { get; } = translations;
}