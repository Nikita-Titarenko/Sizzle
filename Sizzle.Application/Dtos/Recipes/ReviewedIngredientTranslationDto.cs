namespace Sizzle.Application.Dtos.Recipes;

public sealed class ReviewedIngredientTranslationDto
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? Calories { get; init; }
    public Dictionary<string, string> Translations { get; init; } = [];
}