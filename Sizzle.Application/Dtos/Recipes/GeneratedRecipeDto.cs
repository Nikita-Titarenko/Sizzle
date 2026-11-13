namespace Sizzle.Application.Dtos.Recipes;

public class GeneratedRecipeDto
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CookingTimeMinutes { get; set; }

    public Guid CategoryId { get; set; }

    public List<GeneratedRecipeStepDto> Steps { get; set; } = [];

    public List<GeneratedRecipeIngredientDto> Ingredients { get; set; } = [];
}