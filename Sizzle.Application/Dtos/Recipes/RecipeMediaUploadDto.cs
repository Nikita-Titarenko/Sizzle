namespace Sizzle.Application.Dtos.Recipes;

public class RecipeMediaUploadDto
{
    public required string FileName { get; init; }

    public required long Length { get; init; }

    public required Func<Stream> OpenReadStream { get; init; }
}