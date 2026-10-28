namespace Sizzle.Application.Dtos.Comments;

public class CreateCommentDto
{
    public Guid RecipeId { get; init; }

    public Guid? ParentId { get; init; }

    public Guid UserId { get; init; }

    public string Content { get; init; } = string.Empty;
}