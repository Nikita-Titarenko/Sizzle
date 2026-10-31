using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class DeviceTokenRepository(ApplicationDbContext dbContext) : IDeviceTokenRepository
{
    public Task<DeviceToken?> GetAsync(string token)
    {
        return dbContext.DeviceTokens.FindAsync(token).AsTask();
    }

    public Task<List<string>> GetTokensAsync(IReadOnlyCollection<Guid> userIds)
    {
        return dbContext.DeviceTokens.AsNoTracking()
            .Where(device => userIds.Contains(device.UserId))
            .Select(device => device.Token)
            .ToListAsync();
    }

    public Task RemoveAsync(IReadOnlyCollection<string> tokens)
    {
        return dbContext.DeviceTokens
            .Where(device => tokens.Contains(device.Token))
            .ExecuteDeleteAsync();
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
