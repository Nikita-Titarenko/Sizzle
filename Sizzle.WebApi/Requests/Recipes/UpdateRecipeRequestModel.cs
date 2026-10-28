using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Sizzle.WebApi.Requests.Recipes;

public class UpdateRecipeRequestModel
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    [Range(1, int.MaxValue)]
    public int CookingTimeMinutes { get; init; }

    [BindRequired]
    public Guid CategoryId { get; init; }

    public IFormFile? File { get; init; }

    [Required]
    public RecipeStepRequestModel[] Steps { get; init; } = [];

    [Required]
    public Guid[] IngredientIds { get; init; } = [];

    [Required]
    public string[] NewIngredientNames { get; init; } = [];
}
