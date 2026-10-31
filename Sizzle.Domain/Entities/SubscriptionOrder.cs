namespace Sizzle.Domain.Entities;

public sealed class SubscriptionOrder
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public SubscriptionPlan Plan { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "UAH";

    public DateTime CreatedAt { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsPaid { get; set; }
}