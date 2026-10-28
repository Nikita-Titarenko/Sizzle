using Sizzle.WebApi.Requests.Base;

namespace Sizzle.WebApi.Requests.Recipes;

public class GetRecipesRequestModel : PaginationRequestModel
{
    public Guid[] IngredientIds { get; init; } = [];

    public string? Search { get; init; }
}