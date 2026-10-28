namespace Sizzle.Application.Dtos.Recipes;

public class RecipeLikeDto
{
    public Guid RecipeId { get; init; }

    public bool IsLiked { get; init; }
}
