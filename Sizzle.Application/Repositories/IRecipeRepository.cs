using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IRecipeRepository : IRepository<Recipe>
{
	Task<PageResponse<Recipe>> GetRecipesAsync(GetRecipesDto dto);

	Task<Recipe?> GetWithDetailsAsync(Guid recipeId);

	Task<Recipe?> GetWithAuthorAsync(Guid recipeId, Guid? applicationUserId);

	Task<Recipe?> GetWithFullDetailsAsync(Guid recipeId, Guid? applicationUserId);

	void AddSteps(IEnumerable<RecipeStep> steps);

	void RemoveSteps(IEnumerable<RecipeStep> steps);

	void RemoveIngredients(IEnumerable<RecipeIngredient> ingredients);

	Task<Recipe?> GetOwnedByUserAsync(Guid recipeId, Guid applicationUserId);

	Task<ApplicationUserRecipe?> GetUserRecipeAsync(Guid applicationUserId, Guid recipeId);

	void AddUserRecipe(ApplicationUserRecipe userRecipe);
}
