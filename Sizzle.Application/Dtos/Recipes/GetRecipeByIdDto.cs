namespace Sizzle.Application.Dtos.Recipes;

public class GetRecipeByIdDto
{
    public required Guid Id { get; init; }

    public Guid? ApplicationUserId { get; init; }

    public string LanguageCode { get; init; } = "en";
}
