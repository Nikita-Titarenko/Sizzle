using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IRecipeRepository : IRepository<Recipe>
{
	Task<PageResponse<Recipe>> GetRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);
	Task<PageResponse<Recipe>> GetDraftRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);
	Task<PageResponse<Recipe>> GetFollowingRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<FollowingFeedCacheEntry>> GetFollowingFeedEntriesAsync(Guid followerId, int take, CancellationToken cancellationToken = default);
	Task<ApplicationUser?> GetAuthorForMutationAsync(Guid authorId, CancellationToken cancellationToken = default);

	Task<PageResponse<Recipe>> GetSavedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<(Guid Id, DateTime? SavedAt)>> GetSavedRecipeIdsAsync(GetRecipesDto dto, Guid[] pendingIds, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<RecipeInteractionData>> GetRecentInteractionsAsync(Guid userId, int count, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<RecipeRecommendationData>> GetRecommendationCandidatesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Recipe>> GetByIdsWithAuthorAsync(IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken cancellationToken = default);

	Task<Recipe?> GetWithDetailsAsync(Guid recipeId, Guid? userId = null, CancellationToken cancellationToken = default);

	Task<Recipe?> GetWithAuthorAsync(Guid recipeId, Guid? applicationUserId, CancellationToken cancellationToken = default);

	Task<Recipe?> GetWithFullDetailsAsync(Guid recipeId, Guid? applicationUserId, CancellationToken cancellationToken = default);

	void AddSteps(IEnumerable<RecipeStep> steps);

	void RemoveSteps(IEnumerable<RecipeStep> steps);

	void RemoveIngredients(IEnumerable<RecipeIngredient> ingredients);

	Task<Recipe?> GetOwnedByUserAsync(Guid recipeId, Guid applicationUserId, CancellationToken cancellationToken = default);

	Task<ApplicationUserRecipe?> GetUserRecipeAsync(Guid applicationUserId, Guid recipeId, CancellationToken cancellationToken = default);

	Task<bool?> ToggleLikeAsync(Guid applicationUserId, Guid recipeId, CancellationToken cancellationToken = default);

	void AddUserRecipe(ApplicationUserRecipe userRecipe);
}
