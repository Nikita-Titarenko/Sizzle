using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class DeviceTokenRepository(ApplicationDbContext dbContext, ILogger<DeviceTokenRepository>? logger = null) : IDeviceTokenRepository
{
    private readonly ILogger<DeviceTokenRepository> logger = logger ?? NullLogger<DeviceTokenRepository>.Instance;

    public Task<DeviceToken?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.device_tokens.get");
        return dbContext.DeviceTokens.FindAsync([token], cancellationToken).AsTask();
    }

    public Task<List<string>> GetTokensAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.device_tokens.get_for_users");
        return dbContext.DeviceTokens.AsNoTracking()
            .Where(device => userIds.Contains(device.UserId))
            .Select(device => device.Token)
            .ToListAsync(cancellationToken);
    }

    public Task RemoveAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.device_tokens.remove_batch");
        return dbContext.DeviceTokens
            .Where(device => tokens.Contains(device.Token))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void Add(DeviceToken deviceToken)
    {
        logger.LogInformation("db.device_tokens.add");
        dbContext.DeviceTokens.Add(deviceToken);
    }

    public void Remove(DeviceToken deviceToken)
    {
        logger.LogInformation("db.device_tokens.remove");
        dbContext.DeviceTokens.Remove(deviceToken);
    }
}
