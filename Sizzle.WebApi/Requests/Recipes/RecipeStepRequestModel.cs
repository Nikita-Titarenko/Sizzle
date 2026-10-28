namespace Sizzle.WebApi.Requests.Recipes;

public class RecipeStepRequestModel
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IFormFile[] Files { get; init; } = [];
}