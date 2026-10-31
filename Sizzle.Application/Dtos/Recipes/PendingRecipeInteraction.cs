namespace Sizzle.Application.Dtos.Recipes;

public sealed record PendingRecipeInteraction(Guid RecipeId, bool IsLiked, bool IsSaved, bool IsShared, DateTime? ActivityAt);