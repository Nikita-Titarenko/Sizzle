using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sizzle.Application.Options;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Application.Services.Notifications;
using Sizzle.Application.Services.Users;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Options;
using Sizzle.Infrastructure.Repositories;
using Sizzle.Infrastructure.Services;

namespace Sizzle.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfigurationManager configuration, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
            }

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseMySQL(connectionString);
            }
            else
            {
                options.UseInMemoryDatabase("SizzleDb");
            }
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IFileService, FileService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IIngredientUnitFormatter, IngredientUnitFormatter>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IUserFollowerRepository, UserFollowerRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();

        services.AddHttpClient(nameof(FcmPushNotificationSender));
        services.AddSingleton<IPushNotificationSender, FcmPushNotificationSender>();

        services.Configure<SmtpEmailOptions>(configuration.GetSection("EmailOptions"));
        services.Configure<JwtTokenOptions>(configuration.GetSection("Jwt"));
        services.Configure<GoogleOAuthOptions>(configuration.GetSection("GoogleOAuth"));
        services.Configure<BusinessLogicOptions>(configuration.GetSection("BusinessLogic"));
        services.Configure<FirebaseOptions>(configuration.GetSection("Firebase"));

        return services;
    }
}
