using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Application.Services.Subscriptions;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Options;
using Stripe;

namespace Sizzle.Infrastructure.Services;

public sealed class StripeGateway(IOptions<StripeOptions> options, ILogger<StripeGateway> logger) : IStripeGateway
{
    private readonly StripeOptions _options = options.Value;

    public decimal GetPrice(SubscriptionPlan plan)
    {
        ValidatePrices();
        return plan switch
        {
            SubscriptionPlan.Monthly => _options.MonthlyPrice,
            SubscriptionPlan.HalfYearly => _options.HalfYearlyPrice,
            SubscriptionPlan.Yearly => _options.YearlyPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(plan))
        };
    }

    public async Task<SubscriptionCheckoutDto> CreateCheckoutAsync(SubscriptionOrder order)
    {
        ValidatePrices();
        if (string.IsNullOrWhiteSpace(_options.SecretKey) || string.IsNullOrWhiteSpace(_options.PublishableKey))
        {
            throw new InvalidOperationException("Configure Stripe secret and publishable keys.");
        }

        var product = order.Plan switch
        {
            SubscriptionPlan.Monthly => "Sizzle subscription - 1 month",
            SubscriptionPlan.HalfYearly => "Sizzle subscription - 6 months",
            SubscriptionPlan.Yearly => "Sizzle subscription - 12 months",
            _ => throw new ArgumentOutOfRangeException(nameof(order))
        };

        var intent = await new PaymentIntentService(new StripeClient(_options.SecretKey)).CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = checked((long)(order.Amount * 100)),
            Currency = order.Currency.ToLowerInvariant(),
            Description = product,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Metadata = new Dictionary<string, string> { ["orderId"] = order.Id.ToString("N") }
        }, new RequestOptions { IdempotencyKey = order.Id.ToString("N") });

        if (string.IsNullOrWhiteSpace(intent.ClientSecret))
        {
            throw new InvalidOperationException("Stripe did not return a Payment Intent client secret.");
        }

        return new SubscriptionCheckoutDto { OrderId = order.Id, ClientSecret = intent.ClientSecret, PublishableKey = _options.PublishableKey };
    }

    public bool TryParseWebhook(string payload, string signature, out StripePaymentDto? payment)
    {
        payment = null;
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe webhook rejected: {Reason}",
                ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase)
                    ? "signature verification failed"
                    : "invalid event payload or event API version");
            return false;
        }

        if (stripeEvent.Type != "payment_intent.succeeded")
        {
            return true;
        }

        if (stripeEvent.Data.Object is not PaymentIntent intent || intent.Status != "succeeded")
        {
            return true;
        }

        if (intent.Metadata is null
            || !intent.Metadata.TryGetValue("orderId", out var id)
            || !Guid.TryParseExact(id, "N", out var orderId)
            || string.IsNullOrWhiteSpace(intent.Currency))
        {
            return false;
        }

        payment = new StripePaymentDto(orderId, intent.AmountReceived, intent.Currency);
        return true;
    }

    private void ValidatePrices()
    {
        if (_options.MonthlyPrice <= 0 || _options.HalfYearlyPrice <= 0 || _options.YearlyPrice <= 0
            || decimal.Round(_options.MonthlyPrice, 2) != _options.MonthlyPrice
            || decimal.Round(_options.HalfYearlyPrice, 2) != _options.HalfYearlyPrice
            || decimal.Round(_options.YearlyPrice, 2) != _options.YearlyPrice
            || _options.HalfYearlyPrice >= 6 * _options.MonthlyPrice
            || _options.YearlyPrice >= 2 * _options.HalfYearlyPrice)
        {
            throw new InvalidOperationException("Configure positive, discounted Stripe plan prices in UAH with at most two decimal places.");
        }
    }

}