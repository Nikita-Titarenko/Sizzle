namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeListMutation(RecipeListSnapshot? Before, RecipeListSnapshot? After, FeedRecipeDto? Recipe,
    IReadOnlyCollection<Guid>? FollowerIds = null);
