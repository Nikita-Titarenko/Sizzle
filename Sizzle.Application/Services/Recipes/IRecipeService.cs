using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Recipes;

public interface IRecipeService
{
    Task<PageResponse<RecipeDto>> GetRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<PageResponse<FeedRecipeDto>> GetFollowingRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<PageResponse<FeedRecipeDto>> GetSavedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<PageResponse<FeedRecipeDto>> GetPersonalizedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<Result<RecipeDto>> CreateRecipeAsync(CreateRecipeDto dto, CancellationToken cancellationToken = default);

    Task<Result<RecipeDetailsDto>> GetRecipeByIdAsync(GetRecipeByIdDto dto, CancellationToken cancellationToken = default);

    Task<Result<RecipeDto>> UpdateRecipeAsync(UpdateRecipeDto dto, CancellationToken cancellationToken = default);

    Task<Result> DeleteRecipeAsync(Guid recipeId, Guid applicationUserId, CancellationToken cancellationToken = default);

    Task<Result<RecipeLikeDto>> ToggleLikeAsync(ToggleLikeRecipeDto dto, CancellationToken cancellationToken = default);

    Task<Result<RecipeSaveDto>> ToggleSaveAsync(Guid recipeId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<RecipeShareDto>> UpdateShareAsync(Guid recipeId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<FeedRecipeDto>> UpdateViewAsync(UpdateRecipeViewDto dto, CancellationToken cancellationToken = default);

}
