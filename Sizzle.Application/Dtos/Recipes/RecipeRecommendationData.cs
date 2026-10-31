namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeRecommendationData(
    Guid Id,
    Guid AuthorId,
    Guid CategoryId,
    string Name,
    Guid[] IngredientIds);

public sealed record RecipeInteractionData(RecipeRecommendationData Recipe, bool IsLiked, bool IsSaved, bool IsShared);