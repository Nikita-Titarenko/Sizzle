using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;

namespace Sizzle.Infrastructure.Services;

public sealed class DisabledRecipeInteractionBuffer : IRecipeInteractionBuffer
{
    public bool Enabled => false;
    public Task<bool?> ToggleLikeAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RecipeSaveDto?> ToggleSaveAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RecipeShareDto?> UpdateShareAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> UpdateViewAsync(Guid userId, Guid recipeId, int cooldownMinutes, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<(Guid RecipeId, bool IsSaved, DateTime? SavedAt)>> GetPendingSavedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<(Guid, bool, DateTime?)>>([]);
    public Task<IReadOnlyList<PendingRecipeInteraction>> GetPendingInteractionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PendingRecipeInteraction>>([]);
    public Task ApplyAsync(IReadOnlyCollection<RecipeDto> recipes, Guid? userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}