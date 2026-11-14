namespace Sizzle.Application.Dtos.Recipes;

public class RecipeSeedDto
{
    public Guid Id { get; set; }
    public Guid ApplicationUserId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CookingTimeMinutes { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public string? FilePath { get; set; }
    public IngredientSeedDto[] Ingredients { get; set; } = [];
    public StepSeedDto[] Steps { get; set; } = [];
}