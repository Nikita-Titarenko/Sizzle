using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Recipes;

public class UpdateRecipeRequestModel
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IFormFile? File { get; init; }

    [Required]
    public RecipeStepRequestModel[] Steps { get; init; } = [];

    [Required]
    public Guid[] IngredientIds { get; init; } = [];

    [Required]
    public string[] NewIngredientNames { get; init; } = [];
}
