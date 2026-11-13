using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Common;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Cache;

public sealed class RecipeReadCache(IDistributedCache cache, IOptions<RecipeCacheOptions> options, IHttpContextAccessor accessor, IConnectionMultiplexer? connection = null) : IRecipeReadCache
{
    private const string CachePrefix = "sizzle:recipes:cache:entries:v6:";
    private const string RegistryKey = "sizzle:recipes:cache:list-registry:v6";
    private const string LockKey = "sizzle:recipes:cache:list-lock:v6";
    private const string RelatedRegistryKey = "sizzle:recipes:cache:related-registry:v6";
    private readonly IDatabase? redis = connection?.GetDatabase();
    private readonly DistributedCacheEntryOptions entryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.DurationSeconds)
    };

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return await factory(cancellationToken);

        var origin = accessor.HttpContext is { } context
            ? $"{context.Request.Scheme}://{context.Request.Host}"
            : "http://localhost";
        var cacheKey = $"{CachePrefix}{origin}:{key}";
        var cached = await cache.GetAsync(cacheKey, cancellationToken);
        if (cached != null) return JsonSerializer.Deserialize<T>(cached)!;

        var token = await AcquireLockAsync(cancellationToken);
        try
        {
            cached = await cache.GetAsync(cacheKey, cancellationToken);
            if (cached != null) return JsonSerializer.Deserialize<T>(cached)!;

            var result = await factory(cancellationToken);
            await cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(result), entryOptions, cancellationToken);
            if (key.StartsWith("recipes:list:", StringComparison.Ordinal))
            {
                var expiration = DateTimeOffset.UtcNow.AddSeconds(options.Value.DurationSeconds).ToUnixTimeMilliseconds();
                await redis!.SortedSetAddAsync(RegistryKey, JsonSerializer.Serialize(new ListEntry(cacheKey, key, origin)), expiration);
            }
            else if (key.StartsWith("recipes:detail:", StringComparison.Ordinal))
            {
                await RegisterRelatedAsync(cacheKey, key);
            }
            return result;
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return default;

        var origin = accessor.HttpContext is { } context
            ? $"{context.Request.Scheme}://{context.Request.Host}"
            : "http://localhost";
        var cached = await cache.GetAsync($"{CachePrefix}{origin}:{key}", cancellationToken);
        return cached == null ? default : JsonSerializer.Deserialize<T>(cached);
    }

    public async Task<T> GetOrRefreshAsync<T>(string key, Func<T, bool> isUsable, Func<T?, CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default) where T : class
    {
        if (!options.Value.Enabled) return await factory(null, cancellationToken);

        var origin = accessor.HttpContext is { } context
            ? $"{context.Request.Scheme}://{context.Request.Host}"
            : "http://localhost";
        var cacheKey = $"{CachePrefix}{origin}:{key}";
        var cached = await cache.GetAsync(cacheKey, cancellationToken);
        if (cached != null)
        {
            var value = JsonSerializer.Deserialize<T>(cached)!;
            if (isUsable(value)) return value;
        }

        var token = await AcquireLockAsync(cancellationToken);
        try
        {
            cached = await cache.GetAsync(cacheKey, cancellationToken);
            var previous = cached == null ? null : JsonSerializer.Deserialize<T>(cached);
            if (previous != null && isUsable(previous)) return previous;

            var result = await factory(previous, cancellationToken);
            await cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(result), entryOptions, cancellationToken);
            if (key.StartsWith("recipes:personalized:feed:", StringComparison.Ordinal))
            {
                await RegisterRelatedAsync(cacheKey, key);
            }
            return result;
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    public async Task UpdateListsAsync(RecipeListMutation mutation, Func<string, RecipeDetailsDto>? detailsForLanguage = null, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return;

        var token = await AcquireLockAsync(cancellationToken);
        try
        {
            if (mutation.Before != null && mutation.After != null && !mutation.Before.IsDraft && !mutation.After.IsDraft
                && mutation.Recipe != null && detailsForLanguage != null)
                await UpdateRelatedAsync(mutation, detailsForLanguage, cancellationToken);
            else
                await InvalidateRelatedAsync(mutation.After?.Id ?? mutation.Before?.Id, cancellationToken);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await redis!.SortedSetRemoveRangeByScoreAsync(RegistryKey, double.NegativeInfinity, now);
            var entries = new List<(ListEntry Entry, GetRecipesDto Query, PageResponse<RecipeDto> Page, double ExpiresAt)>();
            foreach (var item in await redis.SortedSetRangeByRankWithScoresAsync(RegistryKey))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = JsonSerializer.Deserialize<ListEntry>((string)item.Element!)!;
                var bytes = await cache.GetAsync(entry.CacheKey, cancellationToken);
                if (bytes == null)
                {
                    await redis.SortedSetRemoveAsync(RegistryKey, item.Element);
                    continue;
                }
                entries.Add((entry, JsonSerializer.Deserialize<GetRecipesDto>(entry.Key[13..])!,
                    JsonSerializer.Deserialize<PageResponse<RecipeDto>>(bytes)!, item.Score));
            }

            foreach (var group in entries.GroupBy(item => new
            {
                item.Entry.Origin,
                item.Query.ApplicationUserId,
                item.Query.OwnerId,
                item.Query.Search,
                Categories = string.Join(',', item.Query.CategoryIds),
                Ingredients = string.Join(',', item.Query.IngredientIds),
                item.Query.PageSize
            }))
            {
                var query = group.First().Query;
                if (!RecipeListPages.Matches(mutation.Before, query) && !RecipeListPages.Matches(mutation.After, query)) continue;
                var pages = group.ToDictionary(item => item.Query.PageIndex, item => item.Page);
                var updated = RecipeListPages.Update(pages, query, mutation, group.Key.Origin);
                foreach (var item in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!updated.TryGetValue(item.Query.PageIndex, out var page))
                    {
                        if (detailsForLanguage != null && mutation.Before != null && mutation.After != null)
                        {
                            var beforeMatches = RecipeListPages.Matches(mutation.Before, item.Query);
                            var afterMatches = RecipeListPages.Matches(mutation.After, item.Query);
                            var data = item.Page.Data.Where(recipe => recipe.Id != mutation.After.Id).ToList();
                            if (afterMatches && item.Page.Data.Any(recipe => recipe.Id == mutation.After.Id))
                                data.Add(RecipeListPages.PublicRecipe(mutation.Recipe!, item.Entry.Origin));
                            page = new PageResponse<RecipeDto>
                            {
                                Data = data.OrderByDescending(recipe => recipe.LikeCount)
                                    .ThenByDescending(recipe => recipe.CreationDate).ThenBy(recipe => recipe.Id).ToArray(),
                                TotalSize = item.Page.TotalSize + (afterMatches ? 1 : 0) - (beforeMatches ? 1 : 0),
                                PageIndex = item.Page.PageIndex,
                                PageSize = item.Page.PageSize
                            };
                        }
                        else
                        {
                            await cache.RemoveAsync(item.Entry.CacheKey, cancellationToken);
                            await redis.SortedSetRemoveAsync(RegistryKey, JsonSerializer.Serialize(item.Entry));
                            continue;
                        }
                    }
                    var remaining = TimeSpan.FromMilliseconds(item.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    if (remaining <= TimeSpan.Zero) continue;
                    await cache.SetAsync(item.Entry.CacheKey, JsonSerializer.SerializeToUtf8Bytes(page),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining }, cancellationToken);
                }
            }
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    public async Task UpdateFollowAsync(Guid followerId, Guid followingId, bool isFollowing,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return;

        var token = await AcquireLockAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await redis.SortedSetRemoveRangeByScoreAsync(RelatedRegistryKey, double.NegativeInfinity, now);
            foreach (var member in await redis.SortedSetRangeByRankAsync(RelatedRegistryKey))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = JsonSerializer.Deserialize<RelatedEntry>((string)member!)!;
                if (entry.Key.StartsWith("recipes:detail:", StringComparison.Ordinal))
                {
                    var parts = entry.Key.Split(':');
                    if (!Guid.TryParse(parts[3], out var viewerId) || viewerId != followerId) continue;
                }
                else if (entry.Key.StartsWith("recipes:personalized:feed:", StringComparison.Ordinal))
                {
                    using var query = JsonDocument.Parse(entry.Key[entry.Key.IndexOf('{')..]);
                    if (query.RootElement.GetProperty("ApplicationUserId").GetGuid() != followerId) continue;
                }
                else continue;

                var bytes = await cache.GetAsync(entry.CacheKey, cancellationToken);
                var remaining = await redis.KeyTimeToLiveAsync(entry.CacheKey);
                if (bytes == null || remaining is not { Ticks: > 0 })
                {
                    await redis.SortedSetRemoveAsync(RelatedRegistryKey, member);
                    continue;
                }

                var value = JsonNode.Parse(bytes)!;
                if (entry.Key.StartsWith("recipes:detail:", StringComparison.Ordinal))
                {
                    var recipe = value["Value"];
                    if (recipe == null || recipe["UserId"]?.GetValue<Guid>() != followingId) continue;
                    recipe["IsFollowed"] = isFollowing;
                }
                else if (!UpdateFollowing(value["Data"]!.AsArray(), followingId, isFollowing)) continue;

                await WriteFollowAsync(entry.CacheKey, value, remaining.Value, cancellationToken);
            }
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    private static bool UpdateFollowing(JsonArray recipes, Guid authorId, bool isFollowing)
    {
        var changed = false;
        foreach (var recipe in recipes)
        {
            if (recipe?["UserId"]?.GetValue<Guid>() != authorId) continue;
            recipe["IsFollowed"] = isFollowing;
            changed = true;
        }
        return changed;
    }

    private Task WriteFollowAsync(string key, JsonNode value, TimeSpan remaining, CancellationToken cancellationToken) =>
        cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining }, cancellationToken);

    private Task RegisterRelatedAsync(string cacheKey, string key)
    {
        var expiration = DateTimeOffset.UtcNow.AddSeconds(options.Value.DurationSeconds).ToUnixTimeMilliseconds();
        return redis!.SortedSetAddAsync(RelatedRegistryKey, JsonSerializer.Serialize(new RelatedEntry(cacheKey, key)), expiration);
    }

    private async Task UpdateRelatedAsync(RecipeListMutation mutation, Func<string, RecipeDetailsDto> detailsForLanguage,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await redis!.SortedSetRemoveRangeByScoreAsync(RelatedRegistryKey, double.NegativeInfinity, now);
        foreach (var member in await redis.SortedSetRangeByRankAsync(RelatedRegistryKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = JsonSerializer.Deserialize<RelatedEntry>((string)member!)!;
            var remaining = await redis.KeyTimeToLiveAsync(entry.CacheKey);
            if (remaining is not { Ticks: > 0 })
            {
                await redis.SortedSetRemoveAsync(RelatedRegistryKey, member);
                continue;
            }

            var bytes = await cache.GetAsync(entry.CacheKey, cancellationToken);
            if (bytes == null) continue;
            var origin = entry.CacheKey[..^(entry.Key.Length + 1)][CachePrefix.Length..];
            if (entry.Key.StartsWith($"recipes:detail:{mutation.After!.Id:N}:", StringComparison.Ordinal))
            {
                var previous = JsonSerializer.Deserialize<Result<RecipeDetailsDto>>(bytes)?.Value;
                var language = entry.Key[(entry.Key.LastIndexOf(':') + 1)..];
                var next = detailsForLanguage(language);
                var updated = new RecipeDetailsDto
                {
                    Id = next.Id, UserId = next.UserId, UserName = next.UserName,
                    UserImageUrl = RecipeListPages.Rebase(next.UserImageUrl, origin),
                    IsFollowed = previous?.IsFollowed ?? next.IsFollowed,
                    Name = next.Name, Description = next.Description, CookingTimeMinutes = next.CookingTimeMinutes,
                    CategoryId = next.CategoryId, IsDraft = next.IsDraft, IsAIGeneratedImage = next.IsAIGeneratedImage,
                    FileUrl = RecipeListPages.Rebase(next.FileUrl, origin),
                    ViewCount = next.ViewCount, LikeCount = next.LikeCount, CreationDate = next.CreationDate,
                    IsLiked = previous?.IsLiked ?? next.IsLiked, IsSaved = previous?.IsSaved ?? next.IsSaved,
                    IsSavedAt = previous?.IsSavedAt ?? next.IsSavedAt,
                    Steps = next.Steps.Select(step => new RecipeStepDto
                    {
                        Id = step.Id, Name = step.Name, Description = step.Description,
                        FileUrls = step.FileUrls.Select(url => RecipeListPages.Rebase(url, origin)!).ToArray()
                    }).ToArray(),
                    Ingredients = next.Ingredients
                };
                await cache.SetAsync(entry.CacheKey, JsonSerializer.SerializeToUtf8Bytes(new Result<RecipeDetailsDto> { Value = updated }),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining.Value }, cancellationToken);
            }
            else if (entry.Key.StartsWith("recipes:personalized:feed:", StringComparison.Ordinal))
            {
                var feed = JsonSerializer.Deserialize<PageResponse<FeedRecipeDto>>(bytes)!;
                if (!feed.Data.Any(recipe => recipe.Id == mutation.After!.Id)) continue;
                using var query = JsonDocument.Parse(entry.Key[entry.Key.IndexOf('{')..]);
                var viewerId = query.RootElement.GetProperty("ApplicationUserId").GetGuid();
                var categories = query.RootElement.GetProperty("CategoryIds").EnumerateArray().Select(id => id.GetGuid()).ToArray();
                var ingredients = query.RootElement.GetProperty("IngredientIds").EnumerateArray().Select(id => id.GetGuid()).ToArray();
                var matches = (categories.Length == 0 || categories.Contains(mutation.After.CategoryId))
                    && ingredients.All(id => mutation.After.IngredientIds.Contains(id));
                var recipes = feed.Data.Where(recipe => recipe.Id != mutation.After.Id || matches)
                    .Select(recipe => recipe.Id == mutation.After.Id
                        ? RecipeListPages.ForViewer(mutation.Recipe!, recipe, viewerId, origin) : recipe).ToArray();
                await cache.SetAsync(entry.CacheKey, JsonSerializer.SerializeToUtf8Bytes(new PageResponse<FeedRecipeDto>
                {
                    Data = recipes, TotalSize = feed.TotalSize - (matches ? 0 : 1),
                    PageIndex = feed.PageIndex, PageSize = feed.PageSize
                }), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining.Value }, cancellationToken);
            }
        }
    }

    private async Task InvalidateRelatedAsync(Guid? recipeId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await redis!.SortedSetRemoveRangeByScoreAsync(RelatedRegistryKey, double.NegativeInfinity, now);
        foreach (var member in await redis.SortedSetRangeByRankAsync(RelatedRegistryKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = JsonSerializer.Deserialize<RelatedEntry>((string)member!)!;
            if (!entry.Key.StartsWith("recipes:personalized:feed:", StringComparison.Ordinal)
                && (recipeId == null || !entry.Key.StartsWith($"recipes:detail:{recipeId.Value:N}:", StringComparison.Ordinal)))
                continue;

            await cache.RemoveAsync(entry.CacheKey, cancellationToken);
            await redis.SortedSetRemoveAsync(RelatedRegistryKey, member);
        }
    }

    private async Task<RedisValue> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var token = (RedisValue)Guid.NewGuid().ToString("N");
        while (!await redis!.LockTakeAsync(LockKey, token, TimeSpan.FromMinutes(2)))
            await Task.Delay(25, cancellationToken);
        return token;
    }

    private sealed record ListEntry(string CacheKey, string Key, string Origin);
    private sealed record RelatedEntry(string CacheKey, string Key);
}