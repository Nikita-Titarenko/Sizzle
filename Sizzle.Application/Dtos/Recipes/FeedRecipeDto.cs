namespace Sizzle.Application.Dtos.Recipes;

public class FeedRecipeDto : RecipeDto
{
    public bool IsFollowed { get; init; }

    public bool IsLiked { get; set; }

    public bool IsSaved { get; set; }

    public DateTime? IsSavedAt { get; set; }
}