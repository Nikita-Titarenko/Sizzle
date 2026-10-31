using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Subscriptions;

public interface IStripeGateway
{
    decimal GetPrice(SubscriptionPlan plan);

    Task<SubscriptionCheckoutDto> CreateCheckoutAsync(SubscriptionOrder order);

    bool TryParseWebhook(string payload, string signature, out StripePaymentDto? payment);
}