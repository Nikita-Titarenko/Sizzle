using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class DeviceTokenRepository(ApplicationDbContext dbContext) : IDeviceTokenRepository
{
    public Task<DeviceToken?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        return dbContext.DeviceTokens.FindAsync([token], cancellationToken).AsTask();
    }

    public Task<List<string>> GetTokensAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        return dbContext.DeviceTokens.AsNoTracking()
            .Where(device => userIds.Contains(device.UserId))
            .Select(device => device.Token)
            .ToListAsync(cancellationToken);
    }

    public Task RemoveAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken = default)
    {
        return dbContext.DeviceTokens
            .Where(device => tokens.Contains(device.Token))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void Add(DeviceToken deviceToken)
    {
        dbContext.DeviceTokens.Add(deviceToken);
    }

    public void Remove(DeviceToken deviceToken)
    {
        dbContext.DeviceTokens.Remove(deviceToken);
    }
}
