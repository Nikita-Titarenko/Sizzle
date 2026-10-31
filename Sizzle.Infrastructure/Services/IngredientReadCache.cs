using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Entities;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Services;

public sealed class IngredientReadCache(IDistributedCache cache, IOptions<RecipeCacheOptions> options,
    IIngredientUnitFormatter formatter, IConnectionMultiplexer? connection = null) : IIngredientReadCache
{
    private const string Prefix = "sizzle:ingredients:pages:v1:";
    private const string RegistryKey = "sizzle:ingredients:pages:registry:v1";
    private const string LockKey = "sizzle:ingredients:pages:lock:v1";
    private readonly IDatabase? redis = connection?.GetDatabase();
    private TimeSpan Duration => TimeSpan.FromSeconds(options.Value.DurationSeconds);

    public async Task<List<IngredientListItemDto>> GetOrCreateAsync(GetAllIngredientsDto query,
        Func<Task<List<IngredientListItemDto>>> factory)
    {
        if (!options.Value.Enabled) return await factory();

        var entry = new Entry(Prefix + JsonSerializer.Serialize(query), query);
        var bytes = await cache.GetAsync(entry.Key);
        if (bytes != null) return JsonSerializer.Deserialize<List<IngredientListItemDto>>(bytes)!;

        var token = await AcquireAsync();
        try
        {
            bytes = await cache.GetAsync(entry.Key);
            if (bytes != null) return JsonSerializer.Deserialize<List<IngredientListItemDto>>(bytes)!;
            var value = await factory();
            await cache.SetAsync(entry.Key, JsonSerializer.SerializeToUtf8Bytes(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duration });
            await redis!.SortedSetAddAsync(RegistryKey, JsonSerializer.Serialize(entry),
                DateTimeOffset.UtcNow.Add(Duration).ToUnixTimeMilliseconds());
            return value;
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    public async Task IngredientsCreatedAsync(IReadOnlyCollection<Ingredient> ingredients)
    {
        if (!options.Value.Enabled || ingredients.Count == 0) return;

        var token = await AcquireAsync();
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await redis!.SortedSetRemoveRangeByScoreAsync(RegistryKey, double.NegativeInfinity, now);
            foreach (var member in await redis.SortedSetRangeByScoreAsync(RegistryKey, now + 1))
            {
                var entry = JsonSerializer.Deserialize<Entry>((string)member!)!;
                var bytes = await cache.GetAsync(entry.Key);
                if (bytes == null)
                {
                    await redis.SortedSetRemoveAsync(RegistryKey, member);
                    continue;
                }

                var previous = JsonSerializer.Deserialize<List<IngredientListItemDto>>(bytes)!;
                if (ingredients.Count == 1 && entry.Query.PageIndex == 0
                    && previous.Count == 0 && string.IsNullOrWhiteSpace(entry.Query.Search))
                {
                    var ingredient = ingredients.First();
                    var name = ingredient.Translations.FirstOrDefault(item => item.LanguageCode == entry.Query.LanguageCode)?.Name
                        ?? ingredient.Translations.FirstOrDefault(item => item.LanguageCode == "en")?.Name ?? string.Empty;
                    var remaining = await redis.KeyTimeToLiveAsync(entry.Key);
                    if (remaining is { Ticks: > 0 })
                    {
                        await cache.SetAsync(entry.Key, JsonSerializer.SerializeToUtf8Bytes(new List<IngredientListItemDto>
                        {
                            new() { Id = ingredient.Id, Name = name, Unit = formatter.Format(ingredient.Unit, entry.Query.LanguageCode) }
                        }), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });
                    }
                    continue;
                }

                await cache.RemoveAsync(entry.Key);
                await redis.SortedSetRemoveAsync(RegistryKey, member);
            }
        }
        finally
        {
            await redis!.LockReleaseAsync(LockKey, token);
        }
    }

    private async Task<RedisValue> AcquireAsync()
    {
        var token = (RedisValue)Guid.NewGuid().ToString("N");
        while (!await redis!.LockTakeAsync(LockKey, token, TimeSpan.FromMinutes(2)))
            await Task.Delay(25);
        return token;
    }

    private sealed record Entry(string Key, GetAllIngredientsDto Query);
}
