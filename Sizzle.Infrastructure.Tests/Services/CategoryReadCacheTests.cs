using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Options;
using Sizzle.Infrastructure.Services;

namespace Sizzle.Infrastructure.Tests.Services;

public class CategoryReadCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_CachesSeparatelyByLanguage()
    {
        var storage = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new CategoryReadCache(storage, Microsoft.Extensions.Options.Options.Create(new RecipeCacheOptions { Enabled = true, DurationSeconds = 60 }));
        var calls = 0;
        Task<IReadOnlyList<CategoryDto>> Factory(CancellationToken _) =>
            Task.FromResult<IReadOnlyList<CategoryDto>>([new CategoryDto { Name = $"result-{++calls}" }]);

        var english = await cache.GetOrCreateAsync("en", Factory);
        var englishAgain = await cache.GetOrCreateAsync("en", Factory);
        var ukrainian = await cache.GetOrCreateAsync("uk", Factory);

        Assert.Equal(2, calls);
        Assert.Equal("result-1", Assert.Single(english).Name);
        Assert.Equal("result-1", Assert.Single(englishAgain).Name);
        Assert.Equal("result-2", Assert.Single(ukrainian).Name);
    }

    [Fact]
    public async Task GetOrCreateAsync_DisabledCacheCallsFactoryAndForwardsToken()
    {
        using var source = new CancellationTokenSource();
        var storage = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new CategoryReadCache(storage, Microsoft.Extensions.Options.Options.Create(new RecipeCacheOptions { Enabled = false }));
        var calls = 0;
        Task<IReadOnlyList<CategoryDto>> Factory(CancellationToken token)
        {
            Assert.Equal(source.Token, token);
            calls++;
            return Task.FromResult<IReadOnlyList<CategoryDto>>([]);
        }

        await cache.GetOrCreateAsync("en", Factory, source.Token);
        await cache.GetOrCreateAsync("en", Factory, source.Token);

        Assert.Equal(2, calls);
    }
}