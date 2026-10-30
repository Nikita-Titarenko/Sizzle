namespace Sizzle.Domain.Entities;

public class RecipeIngredient
{
    public Guid RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;

    public Guid IngredientId { get; set; }

    public int Quantity { get; set; }

    public Ingredient Ingredient { get; set; } = null!;
}
