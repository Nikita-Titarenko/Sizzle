namespace Sizzle.Application.Dtos.Recipes;

public sealed class GenerateRecipeFromVideoRequestDto
{
    public RecipeMediaUploadDto? Video { get; init; }

    public string LanguageCode { get; init; } = "en";

    public int FormFieldCount { get; init; }

    public int FileCount { get; init; }
}