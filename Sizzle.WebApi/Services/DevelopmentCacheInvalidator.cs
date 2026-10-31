using StackExchange.Redis;

namespace Sizzle.WebApi.Services;

public static class DevelopmentCacheInvalidator
{
    private static readonly string[] Patterns =
    [
        "sizzle:recipes:cache:entries:*",
        "sizzle:recipes:cache:list-registry:*",
        "sizzle:users:read:v*",
        "sizzle:users:read:registry:*",
        "sizzle:ingredients:pages:v*",
        "sizzle:ingredients:pages:registry:*",
        "sizzle:subscriptions:history:v*",
        "sizzle:comments:pages:v*",
        "sizzle:comments:pages:registry:v*"
    ];

    public static async Task<long> InvalidateAsync(IConnectionMultiplexer connection)
    {
        var database = connection.GetDatabase();
        long deleted = 0;

        foreach (var endpoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica) continue;

            foreach (var pattern in Patterns)
            {
                var matchingKeys = server.Keys(database.Database, pattern, pageSize: 500).ToArray();
                foreach (var keys in matchingKeys.Chunk(500))
                {
                    deleted += await database.KeyDeleteAsync(keys);
                }
            }
        }

        return deleted;
    }
}