namespace Sizzle.Domain.Entities;

public class Recipe
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreationDate { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];

    public ICollection<RecipeStep> RecipeSteps { get; set; } = [];
}
