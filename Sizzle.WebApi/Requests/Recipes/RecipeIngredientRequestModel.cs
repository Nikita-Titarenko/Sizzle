using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Recipes;

public class RecipeIngredientRequestModel
{
    public Guid Id { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
