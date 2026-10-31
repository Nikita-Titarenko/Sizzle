using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Repositories;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class IngredientRepositoryTests : InMemoryRepositoryTest
{
    [Fact]
    public async Task GetAllOrderedAsync_UsesLocalizedNamesEnglishFallbackSearchAndPaging()
    {
        var apple = NewIngredient("Apple", "Яблуко");
        var banana = NewIngredient("Banana");
        var carrot = NewIngredient("Carrot", "Морква");
        await using (var seed = CreateContext())
        {
            seed.Ingredients.AddRange(apple, banana, carrot);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new IngredientRepository(db);
        var ordered = (await repository.GetAllOrderedAsync(null, 0, 2, "uk")).ToList();
        var matched = (await repository.GetAllOrderedAsync("  Ябл  ", null, null, "uk")).ToList();
        var fallback = (await repository.GetAllOrderedAsync("Ban", null, null, "uk")).ToList();

        Assert.Equal([banana.Id, carrot.Id], ordered.Select(ingredient => ingredient.Id));
        Assert.All(ordered, ingredient => Assert.NotEmpty(ingredient.Translations));
        Assert.Equal(apple.Id, Assert.Single(matched).Id);
        Assert.Equal(banana.Id, Assert.Single(fallback).Id);
    }

    [Fact]
    public async Task GetByIdsAndNamesAsync_ReturnOnlyMatchesWithTranslations()
    {
        var apple = NewIngredient("Apple", "Яблуко");
        var banana = NewIngredient("Banana");
        await using (var seed = CreateContext())
        {
            seed.Ingredients.AddRange(apple, banana);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new IngredientRepository(db);

        Assert.Equal(apple.Id, Assert.Single(await repository.GetByIdsAsync([apple.Id])).Id);
        Assert.Equal(2, Assert.Single(await repository.GetByIdsWithTranslationsAsync([apple.Id])).Translations.Count);
        Assert.Equal(banana.Id, Assert.Single(await repository.GetByNamesAsync(["banana"])).Id);
    }

    private static Ingredient NewIngredient(string english, string? ukrainian = null)
    {
        var ingredient = new Ingredient { Id = Guid.NewGuid() };
        ingredient.Translations.Add(new IngredientTranslation { LanguageCode = "en", Name = english });
        if (ukrainian != null)
            ingredient.Translations.Add(new IngredientTranslation { LanguageCode = "uk", Name = ukrainian });
        return ingredient;
    }
}
