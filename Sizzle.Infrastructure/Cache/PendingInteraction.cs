namespace Sizzle.Infrastructure.Cache;

internal sealed class PendingInteraction
{
    public PendingInteraction() { }
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public bool BaseIsLiked { get; set; }
    public bool IsLiked { get; set; }
    public bool IsSaved { get; set; }
    public DateTime? IsSavedAt { get; set; }
    public bool IsShared { get; set; }
    public DateTime? IsSharedAt { get; set; }
    public DateTime? ViewUpdatedAt { get; set; }
    public int ViewDelta { get; set; }
}