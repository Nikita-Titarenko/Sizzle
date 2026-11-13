using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Ingredients;

public class CalculateIngredientProportionsRequestModel
{
    [Required]
    public IEnumerable<IngredientProportionItemRequestModel> Initial { get; init; } = [];

    [Required]
    public IngredientProportionItemRequestModel ChangedIngredient { get; init; } = new();
}