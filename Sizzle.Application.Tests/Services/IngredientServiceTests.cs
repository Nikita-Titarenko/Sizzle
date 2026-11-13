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
        Assert.Equal(42, nested.Calories);
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

    [Fact]
    public void CalculateProportions_HalvesIngredientsAndRoundsHalfEggUp()
    {
        var cheeseId = Guid.NewGuid();
        var eggId = Guid.NewGuid();
        var initial = new List<IngredientProportionItemDto>
        {
            new() { Id = cheeseId, Quantity = 2000 },
            new() { Id = eggId, Quantity = 3 }
        };

        var result = CreateService().CalculateProportions(new CalculateIngredientProportionsDto
        {
            Initial = initial,
            ChangedIngredient = new IngredientProportionItemDto { Id = cheeseId, Quantity = 1000 }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal([cheeseId, eggId], result.Value!.Select(item => item.Id));
        Assert.Equal([1000, 2], result.Value!.Select(item => item.Quantity));
        Assert.Equal([2000, 3], initial.Select(item => item.Quantity));
    }

    [Fact]
    public void CalculateProportions_KeepsChangedQuantityExactAndOtherQuantitiesPositive()
    {
        var cheeseId = Guid.NewGuid();
        var eggId = Guid.NewGuid();

        var result = CreateService().CalculateProportions(new CalculateIngredientProportionsDto
        {
            Initial = [new IngredientProportionItemDto { Id = cheeseId, Quantity = 2000 },
                new IngredientProportionItemDto { Id = eggId, Quantity = 3 }],
            ChangedIngredient = new IngredientProportionItemDto { Id = eggId, Quantity = 1 }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal([667, 1], result.Value!.Select(item => item.Quantity));
    }

    [Fact]
    public void CalculateProportions_RejectsMissingChangedIngredient()
    {
        var result = CreateService().CalculateProportions(new CalculateIngredientProportionsDto
        {
            Initial = [new IngredientProportionItemDto { Id = Guid.NewGuid(), Quantity = 2 }],
            ChangedIngredient = new IngredientProportionItemDto { Id = Guid.NewGuid(), Quantity = 1 }
        });

        Assert.Equal(Sizzle.Domain.Common.ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
    }

    [Fact]
    public void CalculateProportions_RejectsEmptyDuplicateAndNonPositiveQuantities()
    {
        var id = Guid.NewGuid();
        IEnumerable<CalculateIngredientProportionsDto> invalid =
        [
            new() { ChangedIngredient = new IngredientProportionItemDto { Id = id, Quantity = 1 } },
            new() { Initial = [new IngredientProportionItemDto { Id = id, Quantity = 1 },
                new IngredientProportionItemDto { Id = id, Quantity = 2 }],
                ChangedIngredient = new IngredientProportionItemDto { Id = id, Quantity = 1 } },
            new() { Initial = [new IngredientProportionItemDto { Id = id, Quantity = 0 }],
                ChangedIngredient = new IngredientProportionItemDto { Id = id, Quantity = 1 } },
            new() { Initial = [new IngredientProportionItemDto { Id = id, Quantity = 2 }],
                ChangedIngredient = new IngredientProportionItemDto { Id = id, Quantity = 0 } }
        ];

        foreach (var request in invalid)
        {
            var result = CreateService().CalculateProportions(request);

            Assert.Equal(Sizzle.Domain.Common.ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        }
    }

    [Fact]
    public void CalculateProportions_RejectsOverflow()
    {
        var changedId = Guid.NewGuid();
        var result = CreateService().CalculateProportions(new CalculateIngredientProportionsDto
        {
            Initial = [new IngredientProportionItemDto { Id = changedId, Quantity = 1 },
                new IngredientProportionItemDto { Id = Guid.NewGuid(), Quantity = int.MaxValue }],
            ChangedIngredient = new IngredientProportionItemDto { Id = changedId, Quantity = 2 }
        });

        Assert.Equal(Sizzle.Domain.Common.ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
    }

    private static Ingredient NewIngredient(string english, string? ukrainian = null, Guid? parentId = null)
    {
        var ingredient = new Ingredient { Id = Guid.NewGuid(), ParentId = parentId, Unit = IngredientUnit.Grams, Calories = 42 };
        ingredient.Translations.Add(new IngredientTranslation { LanguageCode = "en", Name = english });
        if (ukrainian != null)
            ingredient.Translations.Add(new IngredientTranslation { LanguageCode = "uk", Name = ukrainian });
        return ingredient;
    }
}
