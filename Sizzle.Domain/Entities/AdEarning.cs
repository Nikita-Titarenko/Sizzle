namespace Sizzle.Domain.Entities;

public sealed class AdEarning
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public int AdViewCount { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "usd";

    public string StripeTransferId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
