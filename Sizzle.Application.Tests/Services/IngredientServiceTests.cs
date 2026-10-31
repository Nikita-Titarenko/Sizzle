using NSubstitute;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class IngredientServiceTests
{
    private readonly IIngredientRepository repository = Substitute.For<IIngredientRepository>();
    private readonly IIngredientUnitFormatter formatter = Substitute.For<IIngredientUnitFormatter>();
    private readonly IIngredientReadCache cache = Substitute.For<IIngredientReadCache>();

    private IngredientService CreateService() => new(repository, formatter, cache);

    [Fact]
    public async Task GetAllAsync_UsesLocalizedNamesEnglishFallbackAndBuildsHierarchy()
    {
        var parent = NewIngredient("Vegetables", "Овочі");
        var child = NewIngredient("Carrot", "Морква", parent.Id);
        var englishOnly = NewIngredient("Salt");
        repository.GetAllOrderedAsync(null, 0, 20, "uk", Arg.Any<CancellationToken>())
            .Returns([child, englishOnly, parent]);
        cache.GetOrCreateAsync(Arg.Any<GetAllIngredientsDto>(), Arg.Any<Func<Task<List<IngredientListItemDto>>>>())
            .Returns(call => call.ArgAt<Func<Task<List<IngredientListItemDto>>>>(1)());
        formatter.Format(IngredientUnit.Grams, "uk").Returns("г");
        var query = new GetAllIngredientsDto { LanguageCode = "uk", PageIndex = 0, PageSize = 20 };

        var result = (await CreateService().GetAllAsync(query)).ToList();

        Assert.Equal(2, result.Count);
        var root = Assert.Single(result, item => item.Id == parent.Id);
        Assert.Equal("Овочі", root.Name);
        var nested = Assert.Single(root.Children);
        Assert.Equal(child.Id, nested.Id);
        Assert.Equal("Морква", nested.Name);
        Assert.Equal("г", nested.Unit);
        Assert.Equal("Salt", Assert.Single(result, item => item.Id == englishOnly.Id).Name);
        Assert.DoesNotContain(result, item => item.Id == child.Id);
    }

    [Fact]
    public async Task GetAllAsync_TreatsChildAsRootWhenParentIsNotInPage()
    {
        var child = NewIngredient("Carrot", parentId: Guid.NewGuid());
        repository.GetAllOrderedAsync("Carrot", 1, 1, "en", Arg.Any<CancellationToken>()).Returns([child]);
        cache.GetOrCreateAsync(Arg.Any<GetAllIngredientsDto>(), Arg.Any<Func<Task<List<IngredientListItemDto>>>>())
            .Returns(call => call.ArgAt<Func<Task<List<IngredientListItemDto>>>>(1)());
        var query = new GetAllIngredientsDto { Search = "Carrot", PageIndex = 1, PageSize = 1 };

        var result = await CreateService().GetAllAsync(query);

        Assert.Equal(child.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetAllAsync_OnCacheHit_DoesNotQueryRepository()
    {
        var cached = new List<IngredientListItemDto> { new() { Id = Guid.NewGuid(), Name = "Cached" } };
        cache.GetOrCreateAsync(Arg.Any<GetAllIngredientsDto>(), Arg.Any<Func<Task<List<IngredientListItemDto>>>>())
            .Returns(cached);

        var result = await CreateService().GetAllAsync(new GetAllIngredientsDto());

        Assert.Same(cached, result);
        await repository.DidNotReceive().GetAllOrderedAsync(Arg.Any<string?>(), Arg.Any<int?>(),
            Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static Ingredient NewIngredient(string english, string? ukrainian = null, Guid? parentId = null)
    {
        var ingredient = new Ingredient { Id = Guid.NewGuid(), ParentId = parentId, Unit = IngredientUnit.Grams };
        ingredient.Translations.Add(new IngredientTranslation { LanguageCode = "en", Name = english });
        if (ukrainian != null)
            ingredient.Translations.Add(new IngredientTranslation { LanguageCode = "uk", Name = ukrainian });
        return ingredient;
    }
}
