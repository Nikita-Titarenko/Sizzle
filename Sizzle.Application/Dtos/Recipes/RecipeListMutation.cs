namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeListSnapshot(
    Guid Id,
    Guid OwnerId,
    Guid CategoryId,
    string Name,
    string Description,
    string[] StepTexts,
    Guid[] IngredientIds,
    int LikeCount,
    DateTime CreationDate);

public sealed record RecipeListMutation(RecipeListSnapshot? Before, RecipeListSnapshot? After, RecipeDto? Recipe,
    IReadOnlyCollection<Guid>? FollowerIds = null);
