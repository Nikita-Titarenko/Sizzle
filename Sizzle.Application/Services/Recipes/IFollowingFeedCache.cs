namespace Sizzle.Application.Services.Recipes;

public interface IFollowingFeedCache
{
    Task<IReadOnlyList<FollowingFeedCacheEntry>> GetUserFeedAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task ReplaceUserFeedAsync(Guid userId, IReadOnlyList<FollowingFeedCacheEntry> entries, CancellationToken cancellationToken = default);
    Task AddRecipeToFollowersAsync(Guid authorId, Guid recipeId, DateTime createdAt, IReadOnlyCollection<Guid> followerIds, CancellationToken cancellationToken = default);
    Task AddCelebrityRecipeAsync(Guid authorId, Guid recipeId, DateTime createdAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FollowingFeedCacheEntry>> GetCelebrityRecipesAsync(IReadOnlyCollection<Guid> authorIds, DateTime newerThan, int takePerAuthor, CancellationToken cancellationToken = default);
}
