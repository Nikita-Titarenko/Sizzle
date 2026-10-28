namespace Sizzle.Application.Dtos.Comments;

public class CommentDto
{
    public Guid Id { get; init; }

    public Guid? ParentId { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string? UserImageUrl { get; init; }

    public Guid RecipeId { get; init; }
}