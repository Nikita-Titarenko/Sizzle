namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeRecommendationData(
    Guid Id,
    Guid AuthorId,
    Guid CategoryId,
    string Name,
    Guid[] IngredientIds);