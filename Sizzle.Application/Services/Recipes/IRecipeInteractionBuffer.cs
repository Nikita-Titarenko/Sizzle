using Sizzle.Application.Dtos.Recipes;

namespace Sizzle.Application.Services.Recipes;

public interface IRecipeInteractionBuffer
{
    bool Enabled { get; }
    Task<bool?> ToggleLikeAsync(Guid userId, Guid recipeId);
    Task<RecipeSaveDto?> ToggleSaveAsync(Guid userId, Guid recipeId);
    Task<RecipeShareDto?> UpdateShareAsync(Guid userId, Guid recipeId);
    Task<bool> UpdateViewAsync(Guid userId, Guid recipeId, int cooldownMinutes);
    Task<IReadOnlyList<(Guid RecipeId, bool IsSaved, DateTime? SavedAt)>> GetPendingSavedAsync(Guid userId);
    Task<IReadOnlyList<PendingRecipeInteraction>> GetPendingInteractionsAsync(Guid userId);
    Task ApplyAsync(IReadOnlyCollection<RecipeDto> recipes, Guid? userId);
}