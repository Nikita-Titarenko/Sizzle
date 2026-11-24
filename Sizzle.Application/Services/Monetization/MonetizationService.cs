using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Monetization;
using Sizzle.Application.Options;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Subscriptions;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Monetization;

public sealed class MonetizationService(
    UserManager<ApplicationUser> userManager,
    IUserFollowerRepository followers,
    IRecipeRepository recipes,
    IAdEarningRepository adEarnings,
    IUnitOfWork unitOfWork,
    IStripeGateway stripeGateway,
    IOptions<BusinessLogicOptions> businessLogicOptions,
    ILogger<MonetizationService> logger) : IMonetizationService
{
    private const int AmountDecimalPrecision = 2;

    public async Task<Result> ProcessWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        if (!stripeGateway.TryParseConnectedAccountWebhook(payload, signature, out var connectedAccount))
        {
            return new Result
            {
                Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Invalid Stripe webhook" }]
            };
        }

        if (connectedAccount == null)
        {
            return new Result();
        }

        var user = await userManager.Users.FirstOrDefaultAsync(
            item => item.StripeConnectedAccountId == connectedAccount.ConnectedAccountId,
            cancellationToken);
        if (user == null)
        {
            logger.LogWarning("Connected account webhook received for unknown account {ConnectedAccountId}", connectedAccount.ConnectedAccountId);
            return new Result();
        }

        var isStripeProfileConfigured = connectedAccount.PayoutsEnabled && connectedAccount.ChargesEnabled;
        var isChanged = user.IsStripeProfileConfigured != isStripeProfileConfigured;
        user.IsStripeProfileConfigured = isStripeProfileConfigured;
        if (user.IsStripeProfileConfigured && user.StripeProfileConfiguredAt == null)
        {
            user.StripeProfileConfiguredAt = DateTime.UtcNow;
            isChanged = true;
        }

        if (isChanged)
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }

        return new Result();
    }

    public async Task<Result<ConnectedAccountStatusDto>> GetConnectedAccountStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user == null)
        {
            return new Result<ConnectedAccountStatusDto>
            {
                Errors = [new Error { Key = ErrorKey.NotFound, Message = "User not found" }]
            };
        }

        var status = await BuildStatusAsync(user, cancellationToken);
        return new Result<ConnectedAccountStatusDto> { Value = status };
    }

    public async Task<PageResponse<AdEarningDto>> GetEarningsAsync(Guid userId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var page = await adEarnings.GetByUserAsync(userId, pageIndex, pageSize, cancellationToken);
        return new PageResponse<AdEarningDto>
        {
            Data = page.Data.Select(earning => new AdEarningDto(
                earning.Id,
                earning.AdViewCount,
                earning.Amount,
                earning.Currency,
                earning.StripeTransferId,
                earning.CreatedAt)).ToArray(),
            TotalSize = page.TotalSize,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize
        };
    }

    public async Task ProcessAdEarningsAsync(CancellationToken cancellationToken = default)
    {
        var options = businessLogicOptions.Value;
        if (options.AdPayoutPerView <= 0 || string.IsNullOrWhiteSpace(options.AdPayoutCurrency))
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var previousMonthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        var previousMonthEndUtc = previousMonthStartUtc.AddMonths(1);

        var users = await userManager.Users
            .Where(user => user.IsStripeProfileConfigured && user.StripeConnectedAccountId != null)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!user.StripeProfileConfiguredAt.HasValue)
            {
                logger.LogWarning("Ad earning payout skipped for user {UserId}: StripeProfileConfiguredAt is null", user.Id);
                continue;
            }

            var totalAdViews = await recipes.GetAdViewCountByAuthorAsync(user.Id, user.StripeProfileConfiguredAt.Value, cancellationToken);
            var delta = totalAdViews - user.PaidAdViewCount;
            if (delta <= 0)
            {
                continue;
            }

            var amount = decimal.Round(delta * options.AdPayoutPerView, AmountDecimalPrecision);
            var amountMinor = checked((long)(amount * options.CurrencyMinorMultiplier));
            if (amountMinor <= 0)
            {
                continue;
            }

            string transferId;
            try
            {
                transferId = await stripeGateway.CreateTransferAsync(user.StripeConnectedAccountId!, amountMinor,
                    options.AdPayoutCurrency, new Dictionary<string, string>
                    {
                        ["userId"] = user.Id.ToString("N"),
                        ["adViews"] = delta.ToString(),
                        ["periodStart"] = previousMonthStartUtc.ToString("O"),
                        ["periodEnd"] = previousMonthEndUtc.ToString("O"),
                        ["createdAt"] = nowUtc.ToString("O")
                    }, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ad earning payout failed for user {UserId}", user.Id);
                continue;
            }

            adEarnings.Add(new AdEarning
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AdViewCount = delta,
                Amount = amount,
                Currency = options.AdPayoutCurrency,
                StripeTransferId = transferId,
                CreatedAt = nowUtc
            });

            user.PaidAdViewCount = totalAdViews;

            logger.LogInformation("Ad earning payout created for user {UserId}: {AdViews} views, {Amount} {Currency}",
                user.Id, delta, amount, options.AdPayoutCurrency);
        }

        await unitOfWork.CommitAsync(cancellationToken);
    }

    private async Task<ConnectedAccountStatusDto> BuildStatusAsync(ApplicationUser user, CancellationToken cancellationToken,
        bool? payoutsEnabled = null, bool? chargesEnabled = null)
    {
        var options = businessLogicOptions.Value;
        var sinceUtc = DateTime.UtcNow.AddDays(-options.EligibilityWindowDays);
        var followersCount = await followers.GetFollowerCountAsync(user.Id, cancellationToken);
        var viewsCount = await recipes.GetRecentViewCountByAuthorAsync(user.Id, sinceUtc, cancellationToken);
        var isEligible = followersCount >= options.RequiredFollowersCount && viewsCount >= options.RequiredViewsLast90Days;
        var accountPayoutsEnabled = payoutsEnabled ?? false;
        var accountChargesEnabled = chargesEnabled ?? false;
        string? onboardingLink = null;
        string? expressDashboardLoginLink = null;

        if ((payoutsEnabled == null || chargesEnabled == null) && !string.IsNullOrWhiteSpace(user.StripeConnectedAccountId))
        {
            try
            {
                (accountPayoutsEnabled, accountChargesEnabled) = await stripeGateway.GetConnectedAccountStatusAsync(user.StripeConnectedAccountId, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Stripe account status fetch failed for user {UserId}", user.Id);
            }
        }

        if (string.IsNullOrWhiteSpace(user.StripeConnectedAccountId) && isEligible)
        {
            try
            {
                user.StripeConnectedAccountId = await stripeGateway.CreateConnectedAccountAsync(user.Email ?? string.Empty, cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Stripe connected account creation failed for user {UserId}", user.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(user.StripeConnectedAccountId))
        {
            try
            {
                onboardingLink = await stripeGateway.CreateOnboardingLinkAsync(user.StripeConnectedAccountId, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Stripe onboarding link creation failed for user {UserId}", user.Id);
            }
        }

        if (user.IsStripeProfileConfigured && !string.IsNullOrWhiteSpace(user.StripeConnectedAccountId))
        {
            try
            {
                expressDashboardLoginLink = await stripeGateway.CreateExpressDashboardLoginLinkAsync(user.StripeConnectedAccountId, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Stripe express dashboard login link creation failed for user {UserId}", user.Id);
            }
        }

        return new ConnectedAccountStatusDto(
            isEligible,
            followersCount,
            viewsCount,
            options.RequiredFollowersCount,
            options.RequiredViewsLast90Days,
            user.IsStripeProfileConfigured,
            accountPayoutsEnabled && accountChargesEnabled,
            user.StripeConnectedAccountId,
            onboardingLink,
            expressDashboardLoginLink);
    }
}
