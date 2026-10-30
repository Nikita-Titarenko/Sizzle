using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Sizzle.WebApi.Requests.Recipes;

public class CreateRecipeRequestModel
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
    public RecipeIngredientRequestModel[] Ingredients { get; init; } = [];

    [Required]
    public NewIngredientRequestModel[] NewIngredients { get; init; } = [];
}
