namespace Sizzle.Domain.Entities;

public class Ingredient
{
    public Guid Id { get; set; }

    public IngredientUnit Unit { get; set; }

    public Guid? ParentId { get; set; }

    public Ingredient? Parent { get; set; }

    public ICollection<Ingredient> Children { get; set; } = [];

    public ICollection<IngredientTranslation> Translations { get; set; } = [];

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];
}
