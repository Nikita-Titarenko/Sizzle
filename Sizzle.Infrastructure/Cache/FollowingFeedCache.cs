using System.Text.Json;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Recipes;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Cache;

public sealed class FollowingFeedCache(IOptions<RecipeCacheOptions> options, IConnectionMultiplexer? connection = null) : IFollowingFeedCache
{
    private const string UserFeedPrefix = "sizzle:recipes:following-feed:v1:";
    private const string CelebrityFeedPrefix = "sizzle:recipes:celebrity-feed:v1:";
    private const string UserLockPrefix = "sizzle:recipes:following-feed:lock:v1:";
    private readonly IDatabase? redis = connection?.GetDatabase();

    public async Task<IReadOnlyList<FollowingFeedCacheEntry>> GetUserFeedAsync(Guid userId, int take, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null || take <= 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();
        var values = await redis.SortedSetRangeByRankWithScoresAsync(UserFeedKey(userId), 0, take - 1, Order.Ascending);
        return values.Select(Map).Where(item => item != null).Select(item => item!).ToArray();
    }

    public async Task ReplaceUserFeedAsync(Guid userId, IReadOnlyList<FollowingFeedCacheEntry> entries, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null)
        {
            return;
        }

        var token = await AcquireUserLockAsync(userId, cancellationToken);
        try
        {
            var key = UserFeedKey(userId);
            var transaction = redis.CreateTransaction();
            _ = transaction.KeyDeleteAsync(key);
            foreach (var entry in entries)
            {
                _ = transaction.SortedSetAddAsync(key, Serialize(entry), Score(entry.Recipe.CreationDate));
            }

            _ = transaction.SortedSetRemoveRangeByRankAsync(key, options.Value.FollowingFeedMaxEntries, -1);
            await transaction.ExecuteAsync();
        }
        finally
        {
            await redis.LockReleaseAsync(UserLockKey(userId), token);
        }
    }

    public async Task AddRecipeToFollowersAsync(FeedRecipeDto recipe, IReadOnlyCollection<Guid> followerIds,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null || followerIds.Count == 0)
        {
            return;
        }

        var entry = new FollowingFeedCacheEntry(recipe);
        var value = Serialize(entry);
        var score = Score(recipe.CreationDate);
        foreach (var followerId in followerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = UserFeedKey(followerId);
            var transaction = redis.CreateTransaction();
            _ = transaction.SortedSetAddAsync(key, value, score);
            _ = transaction.SortedSetRemoveRangeByRankAsync(key, options.Value.FollowingFeedMaxEntries, -1);
            await transaction.ExecuteAsync();
        }
    }

    public async Task AddCelebrityRecipeAsync(FeedRecipeDto recipe, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var key = CelebrityFeedKey(recipe.UserId);
        var entry = new FollowingFeedCacheEntry(recipe);
        var transaction = redis.CreateTransaction();
        _ = transaction.SortedSetAddAsync(key, Serialize(entry), Score(recipe.CreationDate));
        _ = transaction.SortedSetRemoveRangeByRankAsync(key, options.Value.FollowingFeedMaxEntries, -1);
        await transaction.ExecuteAsync();
    }

    public async Task RemoveRecipeAsync(Guid authorId, Guid recipeId, IReadOnlyCollection<Guid> followerIds, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null) return;

        foreach (var followerId in followerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = await AcquireUserLockAsync(followerId, cancellationToken);
            try
            {
                var existing = await redis.SortedSetRangeByRankWithScoresAsync(UserFeedKey(followerId), 0, -1, Order.Ascending);
                var matches = existing.Where(item => Map(item)?.Recipe.Id == recipeId).Select(item => item.Element).ToArray();
                if (matches.Length > 0)
                {
                    await redis.SortedSetRemoveAsync(UserFeedKey(followerId), matches);
                }
            }
            finally
            {
                await redis.LockReleaseAsync(UserLockKey(followerId), token);
            }
        }

        var celebrityKey = CelebrityFeedKey(authorId);
        var celebrityExisting = await redis.SortedSetRangeByRankWithScoresAsync(celebrityKey, 0, -1, Order.Ascending);
        var celebrityMatches = celebrityExisting.Where(item => Map(item)?.Recipe.Id == recipeId).Select(item => item.Element).ToArray();
        if (celebrityMatches.Length > 0)
        {
            await redis.SortedSetRemoveAsync(celebrityKey, celebrityMatches);
        }
    }

    public async Task<IReadOnlyList<FollowingFeedCacheEntry>> GetCelebrityRecipesAsync(IReadOnlyCollection<Guid> authorIds, DateTime newerThan,
        int takePerAuthor, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null || authorIds.Count == 0 || takePerAuthor <= 0)
        {
            return [];
        }

        var newerThanScore = Score(newerThan);
        var entries = new List<FollowingFeedCacheEntry>();
        foreach (var authorId in authorIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = await redis.SortedSetRangeByScoreWithScoresAsync(CelebrityFeedKey(authorId),
                double.NegativeInfinity, newerThanScore, Exclude.Stop, Order.Ascending, 0, takePerAuthor);
            entries.AddRange(items.Select(Map).Where(item => item != null).Select(item => item!));
        }

        return entries;
    }

    private static double Score(DateTime createdAt) => -new DateTimeOffset(createdAt).ToUnixTimeMilliseconds();

    private static FollowingFeedCacheEntry? Map(SortedSetEntry item)
    {
        var raw = (string?)item.Element;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = JsonSerializer.Deserialize<FollowingFeedCacheEntry>(raw);
        if (value?.Recipe == null)
        {
            return null;
        }

        return value;
    }

    private static string Serialize(FollowingFeedCacheEntry entry) => JsonSerializer.Serialize(entry);

    private static string UserFeedKey(Guid userId) => $"{UserFeedPrefix}{userId:N}";
    private static string CelebrityFeedKey(Guid authorId) => $"{CelebrityFeedPrefix}{authorId:N}";
    private static string UserLockKey(Guid userId) => $"{UserLockPrefix}{userId:N}";

    private async Task<RedisValue> AcquireUserLockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var token = (RedisValue)Guid.NewGuid().ToString("N");
        while (!await redis!.LockTakeAsync(UserLockKey(userId), token, TimeSpan.FromMinutes(1)))
        {
            await Task.Delay(25, cancellationToken);
        }

        return token;
    }

}
