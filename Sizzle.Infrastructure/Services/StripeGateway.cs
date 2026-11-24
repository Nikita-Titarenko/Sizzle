using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Monetization;
using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Application.Services.Subscriptions;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Options;
using Stripe;

namespace Sizzle.Infrastructure.Services;

public sealed class StripeGateway(IOptions<StripeOptions> options, ILogger<StripeGateway> logger) : IStripeGateway
{
    private const string ConnectedAccountType = "express";
    private const string TransferGroup = "ad-earnings";
    private const string PaymentIntentSucceededEvent = "payment_intent.succeeded";
    private const string ConnectedAccountUpdatedEvent = "account.updated";
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
        if (!TryConstructWebhookEvent(payload, signature, _options.WebhookSecret, out var stripeEvent))
        {
            return false;
        }

        if (stripeEvent.Type != PaymentIntentSucceededEvent)
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

    public bool TryParseConnectedAccountWebhook(string payload, string signature, out StripeConnectedAccountWebhookDto? connectedAccount)
    {
        connectedAccount = null;
        var connectedAccountWebhookSecret = string.IsNullOrWhiteSpace(_options.ConnectedAccountWebhookSecret)
            ? _options.WebhookSecret
            : _options.ConnectedAccountWebhookSecret;
        if (!TryConstructWebhookEvent(payload, signature, connectedAccountWebhookSecret, out var stripeEvent))
        {
            return false;
        }

        if (stripeEvent.Type != ConnectedAccountUpdatedEvent)
        {
            return true;
        }

        if (stripeEvent.Data.Object is not Account account || string.IsNullOrWhiteSpace(account.Id))
        {
            return false;
        }

        connectedAccount = new StripeConnectedAccountWebhookDto(account.Id, account.PayoutsEnabled, account.ChargesEnabled);
        return true;
    }

    public async Task<string> CreateConnectedAccountAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Configure Stripe secret key.");
        }

        var service = new AccountService(new StripeClient(_options.SecretKey));
        var account = await service.CreateAsync(new AccountCreateOptions
        {
            Type = ConnectedAccountType,
            BusinessType = "individual",
            Email = email,
            BusinessProfile = new AccountBusinessProfileOptions
            {
                Mcc = "5815",
                ProductDescription = "Publishing recipes and earning view-based payouts on the platform"
            },
            Capabilities = new AccountCapabilitiesOptions
            {
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
            }
        }, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(account.Id))
        {
            throw new InvalidOperationException("Stripe did not return a connected account id.");
        }

        return account.Id;
    }

    public async Task<string> CreateOnboardingLinkAsync(string connectedAccountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Configure Stripe secret key.");
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectedAccountRefreshUrl) || string.IsNullOrWhiteSpace(_options.ConnectedAccountReturnUrl))
        {
            throw new InvalidOperationException("Configure Stripe connected account refresh and return URLs.");
        }

        var service = new AccountLinkService(new StripeClient(_options.SecretKey));
        var link = await service.CreateAsync(new AccountLinkCreateOptions
        {
            Account = connectedAccountId,
            RefreshUrl = _options.ConnectedAccountRefreshUrl,
            ReturnUrl = _options.ConnectedAccountReturnUrl,
            Type = "account_onboarding"
        }, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(link.Url))
        {
            throw new InvalidOperationException("Stripe did not return an onboarding link.");
        }

        return link.Url;
    }

    public async Task<(bool PayoutsEnabled, bool ChargesEnabled)> GetConnectedAccountStatusAsync(string connectedAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Configure Stripe secret key.");
        }

        var service = new AccountService(new StripeClient(_options.SecretKey));
        var account = await service.GetAsync(connectedAccountId, cancellationToken: cancellationToken);
        return (account.PayoutsEnabled, account.ChargesEnabled);
    }

    public async Task<string> CreateExpressDashboardLoginLinkAsync(string connectedAccountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Configure Stripe secret key.");
        }

        var service = new AccountLoginLinkService(new StripeClient(_options.SecretKey));
        var loginLink = await service.CreateAsync(connectedAccountId, new AccountLoginLinkCreateOptions(), cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(loginLink.Url))
        {
            throw new InvalidOperationException("Stripe did not return an express dashboard login link.");
        }

        return loginLink.Url;
    }

    public async Task<string> CreateTransferAsync(string connectedAccountId, long amountMinor, string currency,
        IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Configure Stripe secret key.");
        }

        if (amountMinor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMinor));
        }

        var service = new TransferService(new StripeClient(_options.SecretKey));
        var transfer = await service.CreateAsync(new TransferCreateOptions
        {
            Destination = connectedAccountId,
            Amount = amountMinor,
            Currency = currency,
            TransferGroup = TransferGroup,
            Metadata = metadata.ToDictionary(entry => entry.Key, entry => entry.Value)
        }, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(transfer.Id))
        {
            throw new InvalidOperationException("Stripe did not return a transfer id.");
        }

        return transfer.Id;
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

    private bool TryConstructWebhookEvent(string payload, string signature, string webhookSecret, out Event stripeEvent)
    {
        stripeEvent = null!;
        if (string.IsNullOrWhiteSpace(webhookSecret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret,
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

        return true;
    }

}