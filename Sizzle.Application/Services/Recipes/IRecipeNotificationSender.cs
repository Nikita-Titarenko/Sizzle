namespace Sizzle.Application.Services.Recipes;

public interface IRecipeNotificationSender
{
    Task RecipeCreatedAsync(Guid recipeId, Guid authorId, string recipeName, DateTime createdAt);
}