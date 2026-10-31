using NSubstitute;
using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Categories;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class CategoryServiceTests
{
    [Fact]
    public async Task GetAllAsync_UsesRequestedLanguageThenEnglishFallbackAndSorts()
    {
        var repository = Substitute.For<ICategoryRepository>();
        var cache = Substitute.For<ICategoryReadCache>();
        cache.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>>>()(call.ArgAt<CancellationToken>(2)));
        var apple = new Category { Id = Guid.NewGuid(), Translations =
        [
            new CategoryTranslation { LanguageCode = "en", Name = "Apple" },
            new CategoryTranslation { LanguageCode = "uk", Name = "Яблуко" }
        ] };
        var banana = new Category { Id = Guid.NewGuid(), Translations =
        [
            new CategoryTranslation { LanguageCode = "en", Name = "Banana" }
        ] };
        repository.GetAllAsync().Returns([apple, banana]);

        var categories = await new CategoryService(repository, cache).GetAllAsync("uk");

        Assert.Equal([banana.Id, apple.Id], categories.Select(category => category.Id));
        Assert.Equal(["Banana", "Яблуко"], categories.Select(category => category.Name));
        await cache.Received(1).GetOrCreateAsync("uk", Arg.Any<Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>>>(), Arg.Any<CancellationToken>());
    }
}
