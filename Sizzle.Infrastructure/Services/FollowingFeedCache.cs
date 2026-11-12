using System.Text.Json;
using Microsoft.Extensions.Options;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Recipes;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Services;

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
                _ = transaction.SortedSetAddAsync(key, Serialize(entry), Score(entry.CreatedAt));
            }

            _ = transaction.SortedSetRemoveRangeByRankAsync(key, options.Value.FollowingFeedMaxEntries, -1);
            await transaction.ExecuteAsync();
        }
        finally
        {
            await redis.LockReleaseAsync(UserLockKey(userId), token);
        }
    }

    public async Task AddRecipeToFollowersAsync(Guid authorId, Guid recipeId, DateTime createdAt, IReadOnlyCollection<Guid> followerIds,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null || followerIds.Count == 0)
        {
            return;
        }

        var value = Serialize(new FollowingFeedCacheEntry(recipeId, authorId, createdAt));
        var score = Score(createdAt);
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

    public async Task AddCelebrityRecipeAsync(Guid authorId, Guid recipeId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || redis == null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var key = CelebrityFeedKey(authorId);
        var transaction = redis.CreateTransaction();
        _ = transaction.SortedSetAddAsync(key, Serialize(new FollowingFeedCacheEntry(recipeId, authorId, createdAt)), Score(createdAt));
        _ = transaction.SortedSetRemoveRangeByRankAsync(key, options.Value.FollowingFeedMaxEntries, -1);
        await transaction.ExecuteAsync();
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

        var value = JsonSerializer.Deserialize<SerializedEntry>(raw);
        if (value == null)
        {
            return null;
        }

        return new FollowingFeedCacheEntry(value.RecipeId, value.AuthorId, DateTimeOffset.FromUnixTimeMilliseconds(-(long)item.Score).UtcDateTime);
    }

    private static string Serialize(FollowingFeedCacheEntry entry) => JsonSerializer.Serialize(new SerializedEntry(entry.RecipeId, entry.AuthorId));

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

    private sealed record SerializedEntry(Guid RecipeId, Guid AuthorId);
}
