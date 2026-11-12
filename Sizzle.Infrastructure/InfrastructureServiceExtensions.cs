using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sizzle.Application.Options;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Application.Services.Categories;
using Sizzle.Application.Services.Comments;
using Sizzle.Application.Services.Recipes;
using Sizzle.Application.Services.Subscriptions;
using Sizzle.Application.Services.Notifications;
using Sizzle.Application.Services.Users;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Options;
using Sizzle.Infrastructure.Repositories;
using Sizzle.Infrastructure.Services;
using StackExchange.Redis;

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
        services.AddOptions<RecipeCacheOptions>()
            .Bind(configuration.GetSection("Cache"))
            .Validate(options => options.DurationSeconds > 0, "Cache:DurationSeconds must be greater than zero.")
            .Validate(options => options.InteractionFlushIntervalMinutes > 0, "Cache:InteractionFlushIntervalMinutes must be greater than zero.")
            .Validate(options => options.FollowingFeedMaxEntries > 0, "Cache:FollowingFeedMaxEntries must be greater than zero.")
            .Validate(options => options.CelebrityFollowerThreshold > 0, "Cache:CelebrityFollowerThreshold must be greater than zero.")
            .Validate(options => options.CelebrityFeedTakePerAuthor > 0, "Cache:CelebrityFeedTakePerAuthor must be greater than zero.")
            .ValidateOnStart();
        services.AddScoped<IRecipeReadCache, RecipeReadCache>();
        services.AddScoped<IFollowingFeedCache, FollowingFeedCache>();
        services.AddScoped<IUserReadCache, UserReadCache>();
        services.AddScoped<ISubscriptionReadCache, SubscriptionReadCache>();
        services.AddScoped<IIngredientReadCache, IngredientReadCache>();
        services.AddScoped<ICategoryReadCache, CategoryReadCache>();
        services.AddScoped<ICommentReadCache, CommentReadCache>();
        services.AddSingleton<IRecipeCreatedEventQueue, RecipeCreatedEventQueue>();
        if (configuration.GetValue<bool>("Cache:Enabled"))
        {
            var redis = configuration["Cache:Redis"] ?? throw new InvalidOperationException("Cache:Redis is required when caching is enabled.");
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
            services.AddScoped<RecipeInteractionBuffer>();
            services.AddScoped<IRecipeInteractionBuffer>(provider => provider.GetRequiredService<RecipeInteractionBuffer>());
            services.AddHostedService<RecipeInteractionFlushService>();
            services.AddHostedService<FollowingFeedCacheWorker>();
        }
        else
        {
            services.AddDistributedMemoryCache();
            services.AddScoped<IRecipeInteractionBuffer, DisabledRecipeInteractionBuffer>();
        }
        services.AddScoped<ISubscriptionOrderRepository, SubscriptionOrderRepository>();
        services.AddScoped<IStripeGateway, StripeGateway>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();

        services.AddHttpClient(nameof(FcmPushNotificationSender));
        services.AddSingleton<IPushNotificationSender, FcmPushNotificationSender>();

        services.Configure<SmtpEmailOptions>(configuration.GetSection("EmailOptions"));
        services.Configure<JwtTokenOptions>(configuration.GetSection("Jwt"));
        services.Configure<GoogleOAuthOptions>(configuration.GetSection("GoogleOAuth"));
        services.Configure<StripeOptions>(configuration.GetSection("Stripe"));
        services.Configure<BusinessLogicOptions>(configuration.GetSection("BusinessLogic"));
        services.Configure<RecipeLoggingOptions>(configuration.GetSection("Logging"));
        services.Configure<FirebaseOptions>(configuration.GetSection("Firebase"));

        return services;
    }
}
