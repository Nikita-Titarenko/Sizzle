namespace Sizzle.Application.Services.Recipes;

public sealed record FollowingFeedCacheEntry(Guid RecipeId, Guid AuthorId, DateTime CreatedAt);
