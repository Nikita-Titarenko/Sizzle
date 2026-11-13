namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeInteractionData(RecipeRecommendationData Recipe, bool IsLiked, bool IsSaved, bool IsShared);