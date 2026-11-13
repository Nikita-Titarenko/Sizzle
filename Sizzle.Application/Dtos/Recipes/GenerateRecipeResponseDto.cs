namespace Sizzle.Application.Dtos.Recipes;

public sealed class GenerateRecipeResponseDto
{
    public GeneratedRecipeDto Recipe { get; init; } = new();

    public string Message { get; init; } = string.Empty;
}