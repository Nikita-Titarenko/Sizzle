using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Comments;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Cache;

public sealed class CommentReadCache(IDistributedCache cache, IOptions<RecipeCacheOptions> options,
    IHttpContextAccessor accessor, IConnectionMultiplexer? connection = null) : ICommentReadCache
{
    private const string Prefix = "sizzle:comments:pages:v1:";
    private const string RegistryPrefix = "sizzle:comments:pages:registry:v1:";
    private const string LockPrefix = "sizzle:comments:pages:lock:v1:";
    private readonly IDatabase? redis = connection?.GetDatabase();
    private TimeSpan Duration => TimeSpan.FromSeconds(options.Value.DurationSeconds);
    private string Origin => accessor.HttpContext is { } context
        ? $"{context.Request.Scheme}://{context.Request.Host}"
        : "http://localhost";

    public async Task<PageResponse<CommentDto>?> GetOrCreateAsync(Guid recipeId, int pageIndex, int pageSize,
        Func<Task<PageResponse<CommentDto>?>> factory)
    {
        if (!options.Value.Enabled) return await factory();

        var origin = Origin;
        var entry = new Entry(Key(recipeId, pageIndex, pageSize, origin), origin, pageIndex, pageSize);
        var bytes = await cache.GetAsync(entry.Key);
        if (bytes != null) return JsonSerializer.Deserialize<PageResponse<CommentDto>>(bytes);

        var token = await AcquireAsync(recipeId);
        try
        {
            bytes = await cache.GetAsync(entry.Key);
            if (bytes != null) return JsonSerializer.Deserialize<PageResponse<CommentDto>>(bytes);

            var page = await factory();
            if (page == null) return null;
            await cache.SetAsync(entry.Key, JsonSerializer.SerializeToUtf8Bytes(page),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duration });
            await redis!.SortedSetAddAsync(RegistryKey(recipeId), JsonSerializer.Serialize(entry),
                DateTimeOffset.UtcNow.Add(Duration).ToUnixTimeMilliseconds());
            return page;
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey(recipeId), token);
        }
    }

    public async Task AddAsync(CommentDto comment)
    {
        if (!options.Value.Enabled) return;

        var token = await AcquireAsync(comment.RecipeId);
        try
        {
            var pages = await ActivePagesAsync(comment.RecipeId);
            foreach (var group in pages.GroupBy(item => new { item.Entry.Origin, item.Entry.PageSize }))
            {
                if (group.Any(item => item.Page.Data.Any(existing => existing.Id == comment.Id))) continue;

                var total = group.First().Page.TotalSize;
                var targetIndex = total / group.Key.PageSize;
                foreach (var (entry, page, remaining) in group)
                {
                    var data = page.Data.ToList();
                    if (entry.PageIndex == targetIndex)
                    {
                        data.Add(WithOrigin(comment, entry.Origin));
                        data = data.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToList();
                    }

                    await WriteAsync(entry, new PageResponse<CommentDto>
                    {
                        Data = data, TotalSize = total + 1,
                        PageIndex = page.PageIndex, PageSize = page.PageSize
                    }, remaining);
                }

                if (total % group.Key.PageSize != 0 || group.Any(item => item.Entry.PageIndex == targetIndex))
                    continue;

                var remainingForNewPage = group.Max(item => item.Remaining);
                var newPage = new Entry(Key(comment.RecipeId, targetIndex, group.Key.PageSize, group.Key.Origin),
                    group.Key.Origin, targetIndex, group.Key.PageSize);
                await WriteAsync(newPage, new PageResponse<CommentDto>
                {
                    Data = [WithOrigin(comment, group.Key.Origin)], TotalSize = total + 1,
                    PageIndex = targetIndex, PageSize = group.Key.PageSize
                }, remainingForNewPage);
                await redis!.SortedSetAddAsync(RegistryKey(comment.RecipeId), JsonSerializer.Serialize(newPage),
                    DateTimeOffset.UtcNow.Add(remainingForNewPage).ToUnixTimeMilliseconds());
            }
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey(comment.RecipeId), token);
        }
    }

    public async Task UpdateAsync(CommentDto comment)
    {
        if (!options.Value.Enabled) return;

        var token = await AcquireAsync(comment.RecipeId);
        try
        {
            foreach (var (entry, page, remaining) in await ActivePagesAsync(comment.RecipeId))
            {
                if (!page.Data.Any(item => item.Id == comment.Id)) continue;
                await WriteAsync(entry, new PageResponse<CommentDto>
                {
                    Data = page.Data.Select(item => item.Id == comment.Id ? WithOrigin(comment, entry.Origin) : item).ToArray(),
                    TotalSize = page.TotalSize, PageIndex = page.PageIndex, PageSize = page.PageSize
                }, remaining);
            }
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey(comment.RecipeId), token);
        }
    }

    public async Task InvalidateAsync(Guid recipeId)
    {
        if (!options.Value.Enabled) return;

        var token = await AcquireAsync(recipeId);
        try
        {
            foreach (var (entry, _, _) in await ActivePagesAsync(recipeId))
                await cache.RemoveAsync(entry.Key);
            await redis!.KeyDeleteAsync(RegistryKey(recipeId));
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey(recipeId), token);
        }
    }

    private async Task<List<(Entry Entry, PageResponse<CommentDto> Page, TimeSpan Remaining)>> ActivePagesAsync(Guid recipeId)
    {
        var registry = RegistryKey(recipeId);
        await redis!.SortedSetRemoveRangeByScoreAsync(registry, double.NegativeInfinity,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var pages = new List<(Entry, PageResponse<CommentDto>, TimeSpan)>();
        foreach (var member in await redis.SortedSetRangeByRankAsync(registry))
        {
            var entry = JsonSerializer.Deserialize<Entry>((string)member!)!;
            var remaining = await redis.KeyTimeToLiveAsync(entry.Key);
            if (remaining is not { Ticks: > 0 })
            {
                await redis.SortedSetRemoveAsync(registry, member);
                continue;
            }
            var bytes = await cache.GetAsync(entry.Key);
            if (bytes != null)
                pages.Add((entry, JsonSerializer.Deserialize<PageResponse<CommentDto>>(bytes)!, remaining.Value));
            else
                await redis.SortedSetRemoveAsync(registry, member);
        }
        return pages;
    }

    private Task WriteAsync(Entry entry, PageResponse<CommentDto> page, TimeSpan remaining) =>
        cache.SetAsync(entry.Key, JsonSerializer.SerializeToUtf8Bytes(page),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });

    private async Task<RedisValue> AcquireAsync(Guid recipeId)
    {
        var token = (RedisValue)Guid.NewGuid().ToString("N");
        while (!await redis!.LockTakeAsync(LockKey(recipeId), token, TimeSpan.FromMinutes(2)))
            await Task.Delay(25);
        return token;
    }

    private static CommentDto WithOrigin(CommentDto comment, string origin) => new()
    {
        Id = comment.Id, ParentId = comment.ParentId, Content = comment.Content,
        CreatedAt = comment.CreatedAt, UserId = comment.UserId, UserName = comment.UserName,
        UserImageUrl = RecipeListPages.Rebase(comment.UserImageUrl, origin), RecipeId = comment.RecipeId
    };

    private static string Key(Guid recipeId, int pageIndex, int pageSize, string origin) =>
        $"{Prefix}{origin}:{recipeId:N}:{pageIndex}:{pageSize}";
    private static string RegistryKey(Guid recipeId) => $"{RegistryPrefix}{recipeId:N}";
    private static string LockKey(Guid recipeId) => $"{LockPrefix}{recipeId:N}";

    private sealed record Entry(string Key, string Origin, int PageIndex, int PageSize);
}
