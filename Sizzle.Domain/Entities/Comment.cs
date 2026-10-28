namespace Sizzle.Domain.Entities;

public class Comment
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public Comment? Parent { get; set; }

    public ICollection<Comment> Replies { get; set; } = [];

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Guid RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;
}