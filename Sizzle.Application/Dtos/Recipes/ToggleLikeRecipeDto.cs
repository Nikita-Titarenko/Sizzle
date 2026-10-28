namespace Sizzle.Application.Dtos.Recipes;

public class ToggleLikeRecipeDto
{
    public required Guid RecipeId { get; init; }

    public required Guid ApplicationUserId { get; init; }
}
