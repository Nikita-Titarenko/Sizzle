namespace Sizzle.Domain.Entities;

public class ApplicationUserRecipe
{
    public Guid ApplicationUserId { get; set; }

    public Guid RecipeId { get; set; }

    public bool IsLiked { get; set; }

    public bool IsSaved { get; set; }

    public DateTime? IsSavedAt { get; set; }

    public bool IsShared { get; set; }

    public DateTime? IsSharedAt { get; set; }

    public DateTime? ViewUpdatedAt { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    public Recipe? Recipe { get; set; }
}