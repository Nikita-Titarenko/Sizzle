using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Application.Dtos.Monetization;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Subscriptions;

public interface IStripeGateway
{
    decimal GetPrice(SubscriptionPlan plan);

    Task<SubscriptionCheckoutDto> CreateCheckoutAsync(SubscriptionOrder order);

    bool TryParseWebhook(string payload, string signature, out StripePaymentDto? payment);

    bool TryParseConnectedAccountWebhook(string payload, string signature, out StripeConnectedAccountWebhookDto? connectedAccount);

    Task<string> CreateConnectedAccountAsync(string email, CancellationToken cancellationToken = default);

    Task<string> CreateOnboardingLinkAsync(string connectedAccountId, CancellationToken cancellationToken = default);

    Task<(bool PayoutsEnabled, bool ChargesEnabled)> GetConnectedAccountStatusAsync(string connectedAccountId, CancellationToken cancellationToken = default);

    Task<string> CreateExpressDashboardLoginLinkAsync(string connectedAccountId, CancellationToken cancellationToken = default);

    Task<string> CreateTransferAsync(string connectedAccountId, long amountMinor, string currency,
        IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default);
}