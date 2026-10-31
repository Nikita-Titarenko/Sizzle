using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Repositories;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class CategoryRepositoryTests : InMemoryRepositoryTest
{
    [Fact]
    public async Task ExistsAsyncAndGetAllAsync_ReturnCategoriesWithTranslations()
    {
        var category = new Category { Id = Guid.NewGuid(), Translations =
        [
            new CategoryTranslation { LanguageCode = "en", Name = "Dessert" },
            new CategoryTranslation { LanguageCode = "uk", Name = "Десерт" }
        ] };
        await using (var seed = CreateContext())
        {
            seed.Categories.Add(category);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new CategoryRepository(db);

        Assert.True(await repository.ExistsAsync(category.Id));
        Assert.False(await repository.ExistsAsync(Guid.NewGuid()));
        var loaded = Assert.Single(await repository.GetAllAsync());
        Assert.Equal(2, loaded.Translations.Count);
        Assert.Contains(loaded.Translations, translation => translation.Name == "Десерт");
    }
}
