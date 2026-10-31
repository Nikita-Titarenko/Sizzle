using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Subscriptions;

public interface ISubscriptionService
{
    IReadOnlyList<SubscriptionPlanDto> GetPlans();

    Task<Result<SubscriptionCheckoutDto>> StartCheckoutAsync(Guid userId, SubscriptionPlan plan);

    Task<Result> ProcessWebhookAsync(string payload, string signature);

    Task<SubscriptionStatusDto> GetStatusAsync(Guid userId);
}