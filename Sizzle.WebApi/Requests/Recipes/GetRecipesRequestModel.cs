using Sizzle.WebApi.Requests.Base;

namespace Sizzle.WebApi.Requests.Recipes;

public class GetRecipesRequestModel : PaginationRequestModel
{
    public Guid? UserId { get; init; }

    public GetRecipeIngredientRequestModel[] Ingredients { get; init; } = [];

    public Guid[] CategoryIds { get; init; } = [];

    public string? Search { get; init; }
}