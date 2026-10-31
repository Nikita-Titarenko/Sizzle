using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Users;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Services;

public sealed class UserReadCache(IDistributedCache cache, IOptions<RecipeCacheOptions> options,
    IHttpContextAccessor accessor, IConnectionMultiplexer? connection = null) : IUserReadCache
{
    private const string Prefix = "sizzle:users:read:v1:";
    private const string RegistryKey = "sizzle:users:read:registry:v1";
    private const string LockKey = "sizzle:users:read:lock:v1";
    private readonly IDatabase? redis = connection?.GetDatabase();
    private TimeSpan Duration => TimeSpan.FromSeconds(options.Value.DurationSeconds);
    private string Origin => accessor.HttpContext is { } context
        ? $"{context.Request.Scheme}://{context.Request.Host}"
        : "http://localhost";

    public Task<UserDto?> GetProfileAsync(Guid userId, Func<Task<UserDto?>> factory) =>
        GetOrCreateAsync("profile", userId, factory);

    public async Task<List<UserDto>> GetFollowersAsync(Guid userId, Func<Task<List<UserDto>>> factory) =>
        await GetOrCreateAsync("followers", userId, factory) ?? [];

    public async Task<List<UserDto>> GetFollowingAsync(Guid userId, Func<Task<List<UserDto>>> factory) =>
        await GetOrCreateAsync("following", userId, factory) ?? [];

    private async Task<T?> GetOrCreateAsync<T>(string kind, Guid userId, Func<Task<T?>> factory)
    {
        if (!options.Value.Enabled) return await factory();

        var origin = Origin;
        var entry = new Entry(Key(kind, userId, origin), kind, userId, origin);
        var bytes = await cache.GetAsync(entry.Key);
        if (bytes != null) return JsonSerializer.Deserialize<T>(bytes);

        var token = await AcquireAsync();
        try
        {
            bytes = await cache.GetAsync(entry.Key);
            if (bytes != null) return JsonSerializer.Deserialize<T>(bytes);
            var value = await factory();
            if (value == null) return value;
            await cache.SetAsync(entry.Key, JsonSerializer.SerializeToUtf8Bytes(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duration });
            await redis!.SortedSetAddAsync(RegistryKey, JsonSerializer.Serialize(entry),
                DateTimeOffset.UtcNow.Add(Duration).ToUnixTimeMilliseconds());
            if (value is List<UserDto> users)
            {
                foreach (var user in users)
                {
                    var profile = new Entry(Key("profile", user.Id, origin), "profile", user.Id, origin);
                    if (await cache.GetAsync(profile.Key) != null) continue;
                    await cache.SetAsync(profile.Key, JsonSerializer.SerializeToUtf8Bytes(user),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duration });
                    await redis.SortedSetAddAsync(RegistryKey, JsonSerializer.Serialize(profile),
                        DateTimeOffset.UtcNow.Add(Duration).ToUnixTimeMilliseconds());
                }
            }
            return value;
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    public async Task UpdateProfileAsync(UserDto user)
    {
        if (!options.Value.Enabled) return;
        var token = await AcquireAsync();
        try
        {
            foreach (var (entry, remaining) in await ActiveEntriesAsync())
            {
                if (entry.Kind == "profile" && entry.UserId == user.Id)
                    await WriteAsync(entry.Key, WithOrigin(user, entry.Origin), remaining);
                else if (entry.Kind is "followers" or "following")
                {
                    var bytes = await cache.GetAsync(entry.Key);
                    if (bytes == null) continue;
                    var list = JsonSerializer.Deserialize<List<UserDto>>(bytes)!;
                    if (!list.Any(item => item.Id == user.Id)) continue;
                    await WriteAsync(entry.Key, list.Select(item => item.Id == user.Id
                        ? WithOrigin(user, entry.Origin) : item).ToList(), remaining);
                }
            }
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    public async Task UpdateFollowAsync(Guid followerId, Guid followingId, bool following)
    {
        if (!options.Value.Enabled) return;
        var token = await AcquireAsync();
        try
        {
            var entries = await ActiveEntriesAsync();
            foreach (var (entry, remaining) in entries.Where(item =>
                item.Entry.Kind == "following" && item.Entry.UserId == followerId
                || item.Entry.Kind == "followers" && item.Entry.UserId == followingId))
            {
                var bytes = await cache.GetAsync(entry.Key);
                if (bytes == null) continue;
                var list = JsonSerializer.Deserialize<List<UserDto>>(bytes)!;
                var memberId = entry.Kind == "following" ? followingId : followerId;
                list.RemoveAll(item => item.Id == memberId);
                if (following)
                {
                    var profileBytes = await cache.GetAsync(Key("profile", memberId, entry.Origin));
                    if (profileBytes == null)
                    {
                        await cache.RemoveAsync(entry.Key);
                        await redis!.SortedSetRemoveAsync(RegistryKey, JsonSerializer.Serialize(entry));
                        continue;
                    }
                    list.Insert(0, JsonSerializer.Deserialize<UserDto>(profileBytes)!);
                }
                await WriteAsync(entry.Key, list, remaining);
            }
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    private async Task<List<(Entry Entry, TimeSpan Remaining)>> ActiveEntriesAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await redis!.SortedSetRemoveRangeByScoreAsync(RegistryKey, double.NegativeInfinity, now);
        var result = new List<(Entry, TimeSpan)>();
        foreach (var item in await redis.SortedSetRangeByScoreWithScoresAsync(RegistryKey, now + 1))
        {
            var entry = JsonSerializer.Deserialize<Entry>((string)item.Element!)!;
            var remaining = await redis.KeyTimeToLiveAsync(entry.Key);
            if (remaining is { Ticks: > 0 }) result.Add((entry, remaining.Value));
            else await redis.SortedSetRemoveAsync(RegistryKey, item.Element);
        }
        return result;
    }

    private Task WriteAsync<T>(string key, T value, TimeSpan remaining) =>
        cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });

    private async Task<RedisValue> AcquireAsync()
    {
        var token = (RedisValue)Guid.NewGuid().ToString("N");
        while (!await redis!.LockTakeAsync(LockKey, token, TimeSpan.FromMinutes(2)))
            await Task.Delay(25);
        return token;
    }

    private static string Key(string kind, Guid userId, string origin) => $"{Prefix}{origin}:{kind}:{userId:N}";

    private static UserDto WithOrigin(UserDto user, string origin) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Description = user.Description,
        Email = user.Email,
        ImageUrl = user.ImageUrl == null ? null : new Uri(new Uri(origin), new Uri(user.ImageUrl).PathAndQuery).ToString()
    };

    private sealed record Entry(string Key, string Kind, Guid UserId, string Origin);
}
