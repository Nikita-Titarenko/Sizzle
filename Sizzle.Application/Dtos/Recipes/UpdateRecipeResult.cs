using Sizzle.Domain.Common;

namespace Sizzle.Application.Dtos.Recipes;

public sealed class UpdateRecipeResult : Result<RecipeDto>
{
    public GeneratedRecipeDto? CorrectedRecipe { get; init; }
}