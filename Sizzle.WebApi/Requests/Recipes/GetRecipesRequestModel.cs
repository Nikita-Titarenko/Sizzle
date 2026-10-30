using Sizzle.WebApi.Requests.Base;

namespace Sizzle.WebApi.Requests.Recipes;

public class GetRecipesRequestModel : PaginationRequestModel
{
    public Guid? UserId { get; init; }

    public Guid[] IngredientIds { get; init; } = [];

    public Guid[] CategoryIds { get; init; } = [];

    public string? Search { get; init; }
}