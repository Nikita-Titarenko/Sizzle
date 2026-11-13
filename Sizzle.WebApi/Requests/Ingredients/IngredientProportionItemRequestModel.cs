using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Ingredients;

public class IngredientProportionItemRequestModel
{
    public Guid Id { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}