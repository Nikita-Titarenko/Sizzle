using StackExchange.Redis;

namespace Sizzle.WebApi.Services;

public static class DevelopmentCacheInvalidator
{
    private const int BatchSize = 500;
    private const string MatchAllPattern = "*";

    public static async Task<long> InvalidateAsync(IConnectionMultiplexer connection)
    {
        var database = connection.GetDatabase();
        long deleted = 0;

        foreach (var endpoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica) continue;

            try
            {
                deleted += server.DatabaseSize(database.Database);
                await server.FlushDatabaseAsync(database.Database);
                continue;
            }
            catch
            {
            }

            var matchingKeys = server.Keys(database.Database, MatchAllPattern, pageSize: BatchSize).ToArray();
            foreach (var keys in matchingKeys.Chunk(BatchSize))
            {
                deleted += await database.KeyDeleteAsync(keys);
            }
        }

        return deleted;
    }
}