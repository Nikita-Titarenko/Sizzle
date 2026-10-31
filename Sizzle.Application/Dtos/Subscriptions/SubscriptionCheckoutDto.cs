namespace Sizzle.Application.Dtos.Subscriptions;

public sealed class SubscriptionCheckoutDto
{
    public Guid OrderId { get; init; }

    public required string ClientSecret { get; init; }

    public required string PublishableKey { get; init; }
}