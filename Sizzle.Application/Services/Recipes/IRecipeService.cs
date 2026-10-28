using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Recipes;

public interface IRecipeService
{
    Task<PageResponse<RecipeDto>> GetRecipesAsync(GetRecipesDto dto);

    Task<Result<RecipeDto>> CreateRecipeAsync(CreateRecipeDto dto);

    Task<Result<RecipeDetailsDto>> GetRecipeByIdAsync(GetRecipeByIdDto dto);

    Task<Result<RecipeDto>> UpdateRecipeAsync(UpdateRecipeDto dto);

    Task<Result> DeleteRecipeAsync(Guid recipeId, Guid applicationUserId);

    Task<Result<RecipeLikeDto>> ToggleLikeAsync(ToggleLikeRecipeDto dto);

    Task<Result<RecipeDto>> UpdateViewAsync(UpdateRecipeViewDto dto);
}
