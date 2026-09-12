namespace Sizzle.Domain.Entities;

public class RecipeStep
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;
}
