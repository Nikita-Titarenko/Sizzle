using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IRecipeRepository : IRepository<Recipe>
{
	Task<PageResponse<Recipe>> GetRecipesAsync(GetRecipesDto dto);
	Task<ApplicationUser?> GetAuthorForMutationAsync(Guid authorId);

	Task<PageResponse<Recipe>> GetSavedRecipesAsync(GetRecipesDto dto);
	Task<IReadOnlyList<(Guid Id, DateTime? SavedAt)>> GetSavedRecipeIdsAsync(GetRecipesDto dto, Guid[] pendingIds);

	Task<IReadOnlyList<RecipeInteractionData>> GetRecentInteractionsAsync(Guid userId, int count);

	Task<IReadOnlyList<RecipeRecommendationData>> GetRecommendationCandidatesAsync(GetRecipesDto dto);

	Task<IReadOnlyList<Recipe>> GetByIdsWithAuthorAsync(IReadOnlyCollection<Guid> ids, Guid userId);

	Task<Recipe?> GetWithDetailsAsync(Guid recipeId, Guid? userId = null);

	Task<Recipe?> GetWithAuthorAsync(Guid recipeId, Guid? applicationUserId);

	Task<Recipe?> GetWithFullDetailsAsync(Guid recipeId, Guid? applicationUserId);

	void AddSteps(IEnumerable<RecipeStep> steps);

	void RemoveSteps(IEnumerable<RecipeStep> steps);

	void RemoveIngredients(IEnumerable<RecipeIngredient> ingredients);

	Task<Recipe?> GetOwnedByUserAsync(Guid recipeId, Guid applicationUserId);

	Task<ApplicationUserRecipe?> GetUserRecipeAsync(Guid applicationUserId, Guid recipeId);

	Task<bool?> ToggleLikeAsync(Guid applicationUserId, Guid recipeId);

	void AddUserRecipe(ApplicationUserRecipe userRecipe);
}
