using Microsoft.Extensions.Logging;
using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Subscriptions;

public sealed class SubscriptionService(
    ISubscriptionOrderRepository orders,
    IUserFollowerRepository users,
    IStripeGateway gateway,
    ISubscriptionReadCache readCache,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    public IReadOnlyList<SubscriptionPlanDto> GetPlans()
    {
        return Enum.GetValues<SubscriptionPlan>()
            .Select(plan => new SubscriptionPlanDto(
                (int)plan,
                plan.ToString(),
                plan switch
                {
                    SubscriptionPlan.Monthly => 1,
                    SubscriptionPlan.HalfYearly => 6,
                    SubscriptionPlan.Yearly => 12,
                    _ => throw new ArgumentOutOfRangeException(nameof(plan))
                },
                gateway.GetPrice(plan),
                "UAH"))
            .ToArray();
    }

    public async Task<Result<SubscriptionCheckoutDto>> StartCheckoutAsync(Guid userId, SubscriptionPlan plan)
    {
        if (!Enum.IsDefined(plan))
        {
            logger.LogWarning("Checkout rejected for user {UserId}: invalid plan {Plan}", userId, plan);
            return new Result<SubscriptionCheckoutDto>
            {
                Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Unknown subscription plan" }]
            };
        }

        if (!await users.UserExistsAsync(userId))
        {
            logger.LogWarning("Checkout rejected: user {UserId} not found", userId);
            return new Result<SubscriptionCheckoutDto>
            {
                Errors = [new Error { Key = ErrorKey.NotFound, Message = "User not found" }]
            };
        }

        var order = new SubscriptionOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Plan = plan,
            Amount = gateway.GetPrice(plan),
            CreatedAt = DateTime.UtcNow
        };
        await orders.AddAsync(order);
        var checkout = await gateway.CreateCheckoutAsync(order);
        logger.LogInformation("Subscription checkout created for order {OrderId} and user {UserId}", order.Id, userId);
        return new Result<SubscriptionCheckoutDto> { Value = checkout };
    }

    public async Task<Result> ProcessWebhookAsync(string payload, string signature)
    {
        if (!gateway.TryParseWebhook(payload, signature, out var payment))
        {
            logger.LogWarning("Subscription webhook rejected: invalid signature or payload");
            return InvalidWebhook();
        }

        if (payment == null)
        {
            return new Result();
        }

        var order = await orders.GetAsync(payment.OrderId);
        if (order == null || order.Currency != payment.Currency.ToUpperInvariant()
            || order.Amount * 100 != payment.AmountTotal)
        {
            logger.LogWarning("Subscription webhook rejected: order {OrderId} missing or payment mismatch", payment.OrderId);
            return InvalidWebhook();
        }

        var activated = await orders.ActivateAsync(order.Id, DateTime.UtcNow);
        if (activated != null)
        {
            logger.LogInformation("Subscription order {OrderId} activated for user {UserId}", activated.Id, activated.UserId);
            await readCache.UpdateAsync(activated.UserId, new SubscriptionHistoryDto(
                activated.Id, activated.Plan, activated.Amount, activated.Currency,
                activated.StartsAt!.Value, activated.ExpiresAt!.Value));
        }
        return new Result();
    }

    public async Task<SubscriptionStatusDto> GetStatusAsync(Guid userId)
    {
        var history = await readCache.GetHistoryAsync(userId, async () =>
            (await orders.GetPaidByUserAsync(userId)).Select(order => new SubscriptionHistoryDto(
                order.Id, order.Plan, order.Amount, order.Currency,
                order.StartsAt!.Value, order.ExpiresAt!.Value)).ToArray());
        var now = DateTime.UtcNow;
        var latestEnd = history.Count == 0 ? (DateTime?)null : history.Max(order => order.ExpiresAt);
        var remainingSeconds = latestEnd > now
            ? (long)Math.Ceiling((latestEnd.Value - now).TotalSeconds)
            : 0;

        return new SubscriptionStatusDto(remainingSeconds, history);
    }

    private static Result InvalidWebhook() => new()
    {
        Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Invalid Stripe webhook" }]
    };
}