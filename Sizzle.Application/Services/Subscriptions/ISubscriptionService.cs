using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Subscriptions;

public interface ISubscriptionService
{
    IReadOnlyList<SubscriptionPlanDto> GetPlans();

    Task<Result<SubscriptionCheckoutDto>> StartCheckoutAsync(Guid userId, SubscriptionPlan plan, CancellationToken cancellationToken = default);

    Task<Result> ProcessWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);

    Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}