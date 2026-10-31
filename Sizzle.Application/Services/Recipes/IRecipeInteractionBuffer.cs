using Sizzle.Application.Dtos.Recipes;

namespace Sizzle.Application.Services.Recipes;

public interface IRecipeInteractionBuffer
{
    bool Enabled { get; }
    Task<bool?> ToggleLikeAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<RecipeSaveDto?> ToggleSaveAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<RecipeShareDto?> UpdateShareAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<bool> UpdateViewAsync(Guid userId, Guid recipeId, int cooldownMinutes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(Guid RecipeId, bool IsSaved, DateTime? SavedAt)>> GetPendingSavedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingRecipeInteraction>> GetPendingInteractionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ApplyAsync(IReadOnlyCollection<RecipeDto> recipes, Guid? userId, CancellationToken cancellationToken = default);
}