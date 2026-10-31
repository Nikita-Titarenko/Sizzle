using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface ISubscriptionOrderRepository
{
    Task AddAsync(SubscriptionOrder order, CancellationToken cancellationToken = default);

    Task<SubscriptionOrder?> GetAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionOrder>> GetPaidByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SubscriptionOrder?> ActivateAsync(Guid orderId, DateTime now, CancellationToken cancellationToken = default);
}