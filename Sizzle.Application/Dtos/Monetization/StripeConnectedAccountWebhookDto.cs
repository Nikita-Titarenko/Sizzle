namespace Sizzle.Application.Dtos.Monetization;

public sealed record StripeConnectedAccountWebhookDto(string ConnectedAccountId, bool PayoutsEnabled, bool ChargesEnabled);
