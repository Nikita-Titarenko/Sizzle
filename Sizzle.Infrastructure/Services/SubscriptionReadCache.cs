using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Subscriptions;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Services;

public sealed class SubscriptionReadCache(IDistributedCache cache, IOptions<RecipeCacheOptions> options,
    IConnectionMultiplexer? connection = null) : ISubscriptionReadCache
{
    private const string Prefix = "sizzle:subscriptions:history:v1:";
    private readonly IDatabase? redis = connection?.GetDatabase();
    private TimeSpan Duration => TimeSpan.FromSeconds(options.Value.DurationSeconds);

    public async Task<IReadOnlyList<SubscriptionHistoryDto>> GetHistoryAsync(Guid userId,
        Func<Task<IReadOnlyList<SubscriptionHistoryDto>>> factory)
    {
        if (!options.Value.Enabled) return await factory();

        var key = Key(userId);
        var bytes = await cache.GetAsync(key);
        if (bytes != null) return JsonSerializer.Deserialize<List<SubscriptionHistoryDto>>(bytes)!;

        var token = await AcquireAsync(userId);
        try
        {
            bytes = await cache.GetAsync(key);
            if (bytes != null) return JsonSerializer.Deserialize<List<SubscriptionHistoryDto>>(bytes)!;

            var history = await factory();
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(history),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duration });
            return history;
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey(userId), token);
        }
    }

    public async Task UpdateAsync(Guid userId, SubscriptionHistoryDto payment)
    {
        if (!options.Value.Enabled) return;

        var token = await AcquireAsync(userId);
        try
        {
            var key = Key(userId);
            var bytes = await cache.GetAsync(key);
            if (bytes == null) return;

            var history = JsonSerializer.Deserialize<List<SubscriptionHistoryDto>>(bytes)!;
            history.RemoveAll(item => item.OrderId == payment.OrderId);
            history.Add(payment);
            history = history.OrderByDescending(item => item.StartsAt).ToList();
            var remaining = await redis!.KeyTimeToLiveAsync(key);
            if (remaining is not { Ticks: > 0 }) return;
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(history),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey(userId), token);
        }
    }

    private async Task<RedisValue> AcquireAsync(Guid userId)
    {
        var token = (RedisValue)Guid.NewGuid().ToString("N");
        while (!await redis!.LockTakeAsync(LockKey(userId), token, TimeSpan.FromMinutes(2)))
            await Task.Delay(25);
        return token;
    }

    private static string Key(Guid userId) => $"{Prefix}{userId:N}";
    private static string LockKey(Guid userId) => $"sizzle:subscriptions:history:lock:v1:{userId:N}";
}
