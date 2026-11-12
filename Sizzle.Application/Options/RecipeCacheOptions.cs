namespace Sizzle.Application.Options;

public sealed class RecipeCacheOptions
{
    public bool Enabled { get; set; }
    public int DurationSeconds { get; set; } = 60;
    public int InteractionFlushIntervalMinutes { get; set; } = 2;
    public int FollowingFeedMaxEntries { get; set; } = 500;
    public int CelebrityFollowerThreshold { get; set; } = 50000;
    public int CelebrityFeedTakePerAuthor { get; set; } = 20;
    public string Redis { get; set; } = "localhost:6379";
}