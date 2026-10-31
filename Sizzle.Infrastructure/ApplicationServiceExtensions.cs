using Microsoft.Extensions.DependencyInjection;
using Sizzle.Application.Services.Categories;
using Sizzle.Application.Services.Comments;
using Sizzle.Application.Services.Devices;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Application.Services.Recipes;
using Sizzle.Application.Services.Users;

namespace Sizzle.Infrastructure;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserFollowerService, UserFollowerService>();
        services.AddScoped<IDeviceTokenService, DeviceTokenService>();
        return services;
    }
}
