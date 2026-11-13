namespace Sizzle.Application.Dtos.Recipes;

public sealed class GenerateRecipeRequestDto
{
    public string LanguageCode { get; init; } = "en";

    public string Message { get; init; } = string.Empty;

    public GeneratedRecipeDto? Recipe { get; init; }
}