namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeShareDto(Guid RecipeId, bool IsShared, DateTime IsSharedAt);