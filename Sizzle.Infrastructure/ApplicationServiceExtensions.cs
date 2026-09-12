using Microsoft.Extensions.DependencyInjection;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Application.Services.Users;

namespace Sizzle.Infrastructure;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
