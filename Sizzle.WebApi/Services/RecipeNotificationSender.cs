using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Notifications;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Entities;
using Sizzle.WebApi.Hubs;

namespace Sizzle.WebApi.Services;

/// <summary>Tells followers about a new recipe: live over SignalR (open web pages) and as a push
/// notification to their phones.</summary>
public class RecipeNotificationSender(
    IUserFollowerRepository followerRepository,
    IDeviceTokenRepository deviceTokenRepository,
    UserManager<ApplicationUser> userManager,
    IHubContext<RecipeNotificationsHub> hubContext,
    IPushNotificationSender pushNotificationSender,
    ILogger<RecipeNotificationSender> logger) : IRecipeNotificationSender
{
    public const string RecipeCreatedType = "recipe_created";

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
            var authorName = author?.Name ?? string.Empty;
            await hubContext.Clients.Users(followerIds.Select(id => id.ToString()))
                .SendAsync("RecipeCreated", new
                {
                    RecipeId = recipeId,
                    RecipeName = recipeName,
                    AuthorId = authorId,
                    AuthorName = authorName,
                    CreatedAt = createdAt
                });

            // A data message: the app builds the notification text itself, in the user's language.
            var tokens = await deviceTokenRepository.GetTokensAsync(followerIds);
            var invalidTokens = await pushNotificationSender.SendAsync(tokens, new Dictionary<string, string>
            {
                ["type"] = RecipeCreatedType,
                ["recipeId"] = recipeId.ToString(),
                ["recipeName"] = recipeName,
                ["authorId"] = authorId.ToString(),
                ["authorName"] = authorName,
                ["createdAt"] = createdAt.ToString("O")
            });
            if (invalidTokens.Count > 0)
            {
                await deviceTokenRepository.RemoveAsync(invalidTokens);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to notify followers about recipe {RecipeId}", recipeId);
        }
    }
}
