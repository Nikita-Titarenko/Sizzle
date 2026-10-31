using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Devices;

public interface IDeviceTokenService
{
    Task<Result> RegisterAsync(Guid userId, string token, CancellationToken cancellationToken = default);

    Task<Result> UnregisterAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}
