using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetAsync(string token);

    Task<List<string>> GetTokensAsync(IReadOnlyCollection<Guid> userIds);

    Task RemoveAsync(IReadOnlyCollection<string> tokens);

    void Add(DeviceToken deviceToken);

    void Remove(DeviceToken deviceToken);
}
