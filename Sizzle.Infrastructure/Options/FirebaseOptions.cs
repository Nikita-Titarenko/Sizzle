namespace Sizzle.Infrastructure.Options;

public sealed class FirebaseOptions
{
    /// <summary>Firebase project ID, e.g. "sizzle-12345".</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Path to the service account JSON key (Firebase console → Project settings → Service accounts).</summary>
    public string ServiceAccountFile { get; set; } = string.Empty;
}
