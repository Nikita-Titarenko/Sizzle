using Sizzle.Domain.Common;

namespace Sizzle.Application.Dtos.Recipes;

public sealed class CreateRecipeResult : Result<RecipeDto>
{
    public GeneratedRecipeDto? CorrectedRecipe { get; init; }
}