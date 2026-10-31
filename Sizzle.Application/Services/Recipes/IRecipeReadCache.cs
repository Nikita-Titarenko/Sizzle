using Sizzle.Application.Dtos.Recipes;

namespace Sizzle.Application.Services.Recipes;

public interface IRecipeReadCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task<T> GetOrRefreshAsync<T>(string key, Func<T, bool> isUsable, Func<T?, CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default) where T : class;
    Task UpdateListsAsync(RecipeListMutation mutation, Func<string, RecipeDetailsDto>? detailsForLanguage = null, CancellationToken cancellationToken = default);
    Task UpdateFollowAsync(Guid followerId, Guid followingId, bool isFollowing, CancellationToken cancellationToken = default);
}