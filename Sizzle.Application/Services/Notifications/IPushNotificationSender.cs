namespace Sizzle.Application.Services.Notifications;

public interface IPushNotificationSender
{
    /// <summary>Sends a data message to each device token and returns the tokens the push service
    /// reported as no longer valid (app uninstalled, token rotated), so they can be deleted.</summary>
    Task<IReadOnlyCollection<string>> SendAsync(IReadOnlyCollection<string> tokens, IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);
}
