using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class RecipeCaloriesTests
{
    [Fact]
    public void Calculate_UsesPerHundredForGramsAndMillilitersAndPerPieceForPieces()
    {
        IEnumerable<RecipeIngredient> ingredients =
        [
            new() { Quantity = 150, Ingredient = new Ingredient { Unit = IngredientUnit.Grams, Calories = 300 } },
            new() { Quantity = 250, Ingredient = new Ingredient { Unit = IngredientUnit.Milliliters, Calories = 40 } },
            new() { Quantity = 3, Ingredient = new Ingredient { Unit = IngredientUnit.Pieces, Calories = 22 } }
        ];

        Assert.Equal(616m, RecipeCalories.Calculate(ingredients));
        ingredients.First().Quantity = 200;
        Assert.Equal(766m, RecipeCalories.Calculate(ingredients));
    }

    [Fact]
    public void Calculate_PreservesFractionalCalories()
    {
        IEnumerable<RecipeIngredient> ingredients =
        [
            new() { Quantity = 15, Ingredient = new Ingredient { Unit = IngredientUnit.Grams, Calories = 33 } }
        ];

        Assert.Equal(4.95m, RecipeCalories.Calculate(ingredients));
    }
}