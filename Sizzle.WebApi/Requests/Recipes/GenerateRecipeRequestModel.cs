using Sizzle.Application.Dtos.Recipes;

namespace Sizzle.WebApi.Requests.Recipes;

public sealed class GenerateRecipeRequestModel
{
    public string Message { get; set; } = string.Empty;

    public GeneratedRecipeDto? Recipe { get; set; }
}