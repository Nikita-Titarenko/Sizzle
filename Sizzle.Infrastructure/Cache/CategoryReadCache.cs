using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Categories;

namespace Sizzle.Infrastructure.Cache;

public sealed class CategoryReadCache(IDistributedCache cache, IOptions<RecipeCacheOptions> options) : ICategoryReadCache
{
    private const string Prefix = "sizzle:categories:lists:v1:";

    public async Task<IReadOnlyList<CategoryDto>> GetOrCreateAsync(string languageCode,
        Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>> factory, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return await factory(cancellationToken);

        var key = Prefix + languageCode;
        var bytes = await cache.GetAsync(key, cancellationToken);
        if (bytes != null) return JsonSerializer.Deserialize<List<CategoryDto>>(bytes)!;

        var categories = await factory(cancellationToken);
        await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(categories), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.DurationSeconds)
        }, cancellationToken);
        return categories;
    }
}