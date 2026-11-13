using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Recipes;

public static class RecipeCalories
{
    private const int BaseQuantity = 100;

    public static decimal Calculate(IEnumerable<RecipeIngredient> ingredients) =>
        ingredients.Sum(link => link.Ingredient.Unit == IngredientUnit.Pieces
            ? (decimal)link.Quantity * link.Ingredient.Calories
            : (decimal)link.Quantity * link.Ingredient.Calories / BaseQuantity);
}