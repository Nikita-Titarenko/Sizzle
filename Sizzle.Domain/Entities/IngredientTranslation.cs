namespace Sizzle.Domain.Entities;

public class IngredientTranslation
{
    public Guid IngredientId { get; set; }

    public Ingredient Ingredient { get; set; } = null!;

    public string LanguageCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}