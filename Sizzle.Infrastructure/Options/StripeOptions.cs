namespace Sizzle.Infrastructure.Options;

public sealed class StripeOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string ConnectedAccountWebhookSecret { get; set; } = string.Empty;
    public string ConnectedAccountRefreshUrl { get; set; } = string.Empty;
    public string ConnectedAccountReturnUrl { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal HalfYearlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
}