namespace Sizzle.Application.Dtos.Recipes;

public class RecipeDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string? UserImageUrl { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsDraft { get; init; }

    public bool IsAIGeneratedImage { get; init; }

    public int CookingTimeMinutes { get; init; }

    public decimal Calories { get; init; }

    public Guid CategoryId { get; init; }

    public string? FileUrl { get; init; }

    public int ViewCount { get; set; }

    public int LikeCount { get; set; }

    public DateTime CreationDate { get; init; }
}
