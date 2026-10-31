using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetAsync(string token, CancellationToken cancellationToken = default);

    Task<List<string>> GetTokensAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    Task RemoveAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken = default);

    void Add(DeviceToken deviceToken);

    void Remove(DeviceToken deviceToken);
}
