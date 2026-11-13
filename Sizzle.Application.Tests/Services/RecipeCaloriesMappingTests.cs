using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Mapping;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class RecipeCaloriesMappingTests
{
    [Fact]
    public void RecipeDto_CalculatesCaloriesFromIngredientQuantities()
    {
        var files = Substitute.For<IFileService>();
        var configuration = new MapperConfiguration(options => options.AddProfile<ApplicationMappingProfile>(),
            NullLoggerFactory.Instance);
        var mapper = configuration.CreateMapper(type => Activator.CreateInstance(type, files)!);
        var recipe = new Recipe
        {
            RecipeIngredients =
            [
                new RecipeIngredient { Quantity = 200, Ingredient = new Ingredient { Unit = IngredientUnit.Grams, Calories = 300 } },
                new RecipeIngredient { Quantity = 2, Ingredient = new Ingredient { Unit = IngredientUnit.Pieces, Calories = 70 } }
            ]
        };

        var result = mapper.Map<RecipeDto>(recipe);

        Assert.Equal(740m, result.Calories);
        Assert.Equal(740m, mapper.Map<FeedRecipeDto>(recipe).Calories);
    }
}