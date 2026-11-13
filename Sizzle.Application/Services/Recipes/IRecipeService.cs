using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Recipes;

public interface IRecipeService
{
    Task<PageResponse<RecipeDto>> GetRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<PageResponse<RecipeDto>> GetDraftRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<PageResponse<FeedRecipeDto>> GetFollowingRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<PageResponse<FeedRecipeDto>> GetSavedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<PageResponse<FeedRecipeDto>> GetPersonalizedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

    Task<CreateRecipeResult> CreateRecipeAsync(CreateRecipeDto dto, CancellationToken cancellationToken = default);

    IAsyncEnumerable<RecipeGenerationEvent> GenerateRecipeAsync(GenerateRecipeRequestDto request, CancellationToken cancellationToken = default);

    Task<Result<IAsyncEnumerable<RecipeGenerationEvent>>> GenerateRecipeFromVideoAsync(GenerateRecipeFromVideoRequestDto request,
        Guid userId, CancellationToken cancellationToken = default);

    Task<Result<GeneratedRecipeImage>> GenerateRecipeImageAsync(GeneratedRecipeDto? recipe, Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<RecipeDetailsDto>> GetRecipeByIdAsync(GetRecipeByIdDto dto, CancellationToken cancellationToken = default);

    Task<UpdateRecipeResult> UpdateRecipeAsync(UpdateRecipeDto dto, CancellationToken cancellationToken = default);

    Task<Result> DeleteRecipeAsync(Guid recipeId, Guid applicationUserId, CancellationToken cancellationToken = default);

    Task<Result<RecipeLikeDto>> ToggleLikeAsync(ToggleLikeRecipeDto dto, CancellationToken cancellationToken = default);

    Task<Result<RecipeSaveDto>> ToggleSaveAsync(Guid recipeId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<RecipeShareDto>> UpdateShareAsync(Guid recipeId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result> UpdateViewAsync(UpdateRecipeViewDto dto, CancellationToken cancellationToken = default);

}
