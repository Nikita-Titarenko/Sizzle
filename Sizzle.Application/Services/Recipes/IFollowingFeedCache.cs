using Sizzle.Application.Dtos.Recipes;

namespace Sizzle.Application.Services.Recipes;

public interface IFollowingFeedCache
{
    Task<IReadOnlyList<FollowingFeedCacheEntry>> GetUserFeedAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task ReplaceUserFeedAsync(Guid userId, IReadOnlyList<FollowingFeedCacheEntry> entries, CancellationToken cancellationToken = default);
    Task AddRecipeToFollowersAsync(FeedRecipeDto recipe, IReadOnlyCollection<Guid> followerIds, CancellationToken cancellationToken = default);
    Task AddCelebrityRecipeAsync(FeedRecipeDto recipe, CancellationToken cancellationToken = default);
    Task RemoveRecipeAsync(Guid authorId, Guid recipeId, IReadOnlyCollection<Guid> followerIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FollowingFeedCacheEntry>> GetCelebrityRecipesAsync(IReadOnlyCollection<Guid> authorIds, DateTime newerThan, int takePerAuthor, CancellationToken cancellationToken = default);
}
