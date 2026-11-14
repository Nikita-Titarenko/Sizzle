namespace Sizzle.WebApi.Requests.Recipes;

public class GetRecipeIngredientRequestModel
{
    public Guid IngredientId { get; init; }

    public int? Quantity { get; init; }
}