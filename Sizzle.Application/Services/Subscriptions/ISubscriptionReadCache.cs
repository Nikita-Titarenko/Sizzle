using Sizzle.Application.Dtos.Subscriptions;

namespace Sizzle.Application.Services.Subscriptions;

public interface ISubscriptionReadCache
{
    Task<IReadOnlyList<SubscriptionHistoryDto>> GetHistoryAsync(Guid userId,
        Func<Task<IReadOnlyList<SubscriptionHistoryDto>>> factory);

    Task UpdateAsync(Guid userId, SubscriptionHistoryDto payment);
}
