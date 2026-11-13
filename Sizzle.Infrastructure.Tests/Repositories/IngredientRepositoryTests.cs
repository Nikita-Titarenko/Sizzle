using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task GetByIdsWithTranslationsTrackedAsync_KeepsExistingIngredientUnchangedWhenAddingRecipe()
    {
        var ingredient = NewIngredient("Pumpkin", "Гарбуз");
        await using (var seed = CreateContext())
        {
            seed.Ingredients.Add(ingredient);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new IngredientRepository(db);
        var existing = Assert.Single(await repository.GetByIdsWithTranslationsTrackedAsync([ingredient.Id]));
        Assert.Equal(2, existing.Translations.Count);
        Assert.Equal(EntityState.Unchanged, db.Entry(existing).State);

        db.Recipes.Add(new Recipe
        {
            Id = Guid.NewGuid(), CategoryId = Guid.NewGuid(), ApplicationUserId = Guid.NewGuid(),
            RecipeIngredients = [new RecipeIngredient { IngredientId = existing.Id, Ingredient = existing, Quantity = 600 }]
        });

        Assert.Equal(EntityState.Unchanged, db.Entry(existing).State);
        await db.SaveChangesAsync();
        Assert.Single(db.Ingredients);
        Assert.Single(db.RecipeIngredients);
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
