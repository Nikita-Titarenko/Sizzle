using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Options;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Recipes;

namespace Sizzle.Infrastructure.Cache;

public sealed class FollowingFeedCacheWorker(IRecipeCreatedEventQueue queue, IServiceScopeFactory scopeFactory,
    IOptions<RecipeCacheOptions> options, ILogger<FollowingFeedCacheWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var recipes = scope.ServiceProvider.GetRequiredService<IRecipeRepository>();
                if (await recipes.GetWithAuthorAsync(item.RecipeId, null, stoppingToken) == null) continue;

                var followers = scope.ServiceProvider.GetRequiredService<IUserFollowerRepository>();
                var cache = scope.ServiceProvider.GetRequiredService<IFollowingFeedCache>();
                var followerCount = await followers.GetFollowerCountAsync(item.AuthorId, stoppingToken);
                if (followerCount > options.Value.CelebrityFollowerThreshold)
                {
                    await cache.AddCelebrityRecipeAsync(item.AuthorId, item.RecipeId, item.CreatedAt, stoppingToken);
                    continue;
                }

                var followerIds = await followers.GetFollowerIdsAsync(item.AuthorId, stoppingToken);
                await cache.AddRecipeToFollowersAsync(item.AuthorId, item.RecipeId, item.CreatedAt, followerIds, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to update following feed cache.");
            }
        }
    }
}
