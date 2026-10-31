namespace Sizzle.Domain.Entities;

/// <summary>A push notification token (Firebase Cloud Messaging) of a device where the user is signed in.</summary>
public class DeviceToken
{
    public string Token { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }
}
