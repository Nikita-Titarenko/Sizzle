using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Entities;
using Sizzle.WebApi.Hubs;

namespace Sizzle.WebApi.Services;

public class RecipeNotificationSender(
    IUserFollowerRepository followerRepository,
    UserManager<ApplicationUser> userManager,
    IHubContext<RecipeNotificationsHub> hubContext,
    ILogger<RecipeNotificationSender> logger) : IRecipeNotificationSender
{
    public async Task RecipeCreatedAsync(Guid recipeId, Guid authorId, string recipeName, DateTime createdAt)
    {
        try
        {
            var followerIds = await followerRepository.GetFollowerIdsAsync(authorId);
            if (followerIds.Count == 0)
            {
                return;
            }

            var author = await userManager.FindByIdAsync(authorId.ToString());
            await hubContext.Clients.Users(followerIds.Select(id => id.ToString()))
                .SendAsync("RecipeCreated", new
                {
                    RecipeId = recipeId,
                    RecipeName = recipeName,
                    AuthorId = authorId,
                    AuthorName = author?.Name ?? string.Empty,
                    CreatedAt = createdAt
                });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to notify followers about recipe {RecipeId}", recipeId);
        }
    }
}