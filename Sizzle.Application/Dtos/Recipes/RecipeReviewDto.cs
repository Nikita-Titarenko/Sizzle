using System.Text.Json.Serialization;

namespace Sizzle.Application.Dtos.Recipes;

public sealed class RecipeReviewDto
{
    [JsonConverter(typeof(JsonStringEnumConverter<RecipeReviewStatus>))]
    public RecipeReviewStatus Status { get; init; }
    public string Reason { get; init; } = string.Empty;
    public GeneratedRecipeDto? CorrectedRecipe { get; init; }
    public List<ReviewedIngredientTranslationDto> NewIngredients { get; init; } = [];
}
