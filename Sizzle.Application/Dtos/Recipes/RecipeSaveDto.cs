namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeSaveDto(Guid RecipeId, bool IsSaved, DateTime? IsSavedAt);