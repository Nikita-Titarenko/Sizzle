using Sizzle.Domain.Common;

namespace Sizzle.Application.Dtos.Recipes;

public sealed class RecipeReviewOutcomeDto(IReadOnlyCollection<ApprovedNewIngredientDto> approvedIngredients,
    Error? error = null, GeneratedRecipeDto? correctedRecipe = null)
{
    public IReadOnlyCollection<ApprovedNewIngredientDto> ApprovedIngredients { get; } = approvedIngredients;

    public Error? Error { get; } = error;

    public GeneratedRecipeDto? CorrectedRecipe { get; } = correctedRecipe;
}