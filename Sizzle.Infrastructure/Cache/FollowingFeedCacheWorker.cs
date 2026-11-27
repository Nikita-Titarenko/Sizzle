using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Recipes;
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
                var recipe = await recipes.GetWithAuthorAsync(item.RecipeId, null, stoppingToken);
                if (recipe == null) continue;

                var feedRecipe = new FeedRecipeDto
                {
                    Id = recipe.Id,
                    UserId = recipe.ApplicationUserId,
                    UserName = recipe.ApplicationUser?.Name ?? string.Empty,
                    UserImageUrl = null,
                    Name = recipe.Name,
                    Description = recipe.Description,
                    IsDraft = recipe.IsDraft,
                    IsAIGeneratedImage = recipe.IsAIGeneratedImage,
                    CookingTimeMinutes = recipe.CookingTimeMinutes,
                    Calories = RecipeCalories.Calculate(recipe.RecipeIngredients),
                    CategoryId = recipe.CategoryId,
                    FileUrl = null,
                    ViewCount = recipe.ViewCount,
                    LikeCount = recipe.LikeCount,
                    CreationDate = recipe.CreationDate,
                    IsFollowed = true,
                    IsLiked = false,
                    IsSaved = false,
                    IsSavedAt = null
                };

                var followers = scope.ServiceProvider.GetRequiredService<IUserFollowerRepository>();
                var cache = scope.ServiceProvider.GetRequiredService<IFollowingFeedCache>();
                var followerCount = await followers.GetFollowerCountAsync(item.AuthorId, stoppingToken);
                if (followerCount > options.Value.CelebrityFollowerThreshold)
                {
                    await cache.AddCelebrityRecipeAsync(feedRecipe, stoppingToken);
                    continue;
                }

                var followerIds = await followers.GetFollowerIdsAsync(item.AuthorId, stoppingToken);
                await cache.AddRecipeToFollowersAsync(feedRecipe, followerIds, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to update following feed cache.");
            }
        }
    }
}
