using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface ISubscriptionOrderRepository
{
    Task AddAsync(SubscriptionOrder order);

    Task<SubscriptionOrder?> GetAsync(Guid orderId);

    Task<IReadOnlyList<SubscriptionOrder>> GetPaidByUserAsync(Guid userId);

    Task<SubscriptionOrder?> ActivateAsync(Guid orderId, DateTime now);
}