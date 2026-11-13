namespace Sizzle.Application.Dtos;

public sealed record RecipeCreatedEventDto(Guid RecipeId, Guid AuthorId, DateTime CreatedAt);