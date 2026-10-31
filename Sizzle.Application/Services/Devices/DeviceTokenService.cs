using Microsoft.Extensions.Logging;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Devices;

public class DeviceTokenService(IDeviceTokenRepository deviceTokenRepository, IUnitOfWork unitOfWork,
    ILogger<DeviceTokenService> logger) : IDeviceTokenService
{
    public const int MaxTokenLength = 512;

    /// <summary>Stores the token for the user. A token that belonged to another account moves to this one,
    /// so a shared phone only gets notifications for whoever signed in last.</summary>
    public async Task<Result> RegisterAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            logger.LogWarning("Device registration rejected for user {UserId}: invalid token", userId);
            return new Result { Errors = [new Error { Key = ErrorKey.BadRequest, Field = "Token", Message = "Invalid device token" }] };
        }

        var existing = await deviceTokenRepository.GetAsync(token, cancellationToken);
        if (existing == null)
        {
            deviceTokenRepository.Add(new DeviceToken { Token = token, UserId = userId, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            existing.UserId = userId;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("Device registered for user {UserId}", userId);
        return new Result();
    }

    public async Task<Result> UnregisterAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var existing = await deviceTokenRepository.GetAsync(token, cancellationToken);
        if (existing == null || existing.UserId != userId)
        {
            logger.LogWarning("Device removal rejected for user {UserId}: token not found", userId);
            return new Result { Errors = [new Error { Key = ErrorKey.NotFound, Message = "Device token not found" }] };
        }

        deviceTokenRepository.Remove(existing);
        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("Device removed for user {UserId}", userId);
        return new Result();
    }
}
