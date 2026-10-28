namespace Sizzle.Domain.Entities;

public class Recipe
{
    public Guid Id { get; set; }

    public Guid ApplicationUserId { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CookingTimeMinutes { get; set; }

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public string? FilePath { get; set; }

    public int ViewCount { get; set; }

    public DateTime CreationDate { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];

    public ICollection<RecipeStep> RecipeSteps { get; set; } = [];

    public ICollection<ApplicationUserRecipe> ApplicationUserRecipes { get; set; } = [];

    public ICollection<Comment> Comments { get; set; } = [];
}
