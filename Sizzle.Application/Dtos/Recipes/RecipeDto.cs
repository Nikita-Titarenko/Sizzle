namespace Sizzle.Application.Dtos.Recipes;

public class RecipeDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string? UserImageUrl { get; init; }

    public bool IsFollowed { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int CookingTimeMinutes { get; init; }

    public Guid CategoryId { get; init; }

    public string? FileUrl { get; init; }

    public int ViewCount { get; init; }

    public DateTime CreationDate { get; init; }

    public bool IsLiked { get; init; }
}
