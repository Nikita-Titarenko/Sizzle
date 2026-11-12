using System.Text;
using Sizzle.WebApi.Mapping;
using Sizzle.Application.Mapping;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Filters;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure;
using Sizzle.Application.Services.Recipes;
using Sizzle.Application.Services;
using Sizzle.WebApi.Hubs;
using Sizzle.WebApi.ServiceExtensions;
using Sizzle.WebApi.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);

if (builder.Configuration.GetValue<bool>("Logging:PersonalizedRecipesLogging"))
{
    var category = typeof(PersonalizedRecipesLog).FullName!;
    builder.Logging.AddFilter<ConsoleLoggerProvider>(category, LogLevel.None);
    builder.Logging.AddFilter<DebugLoggerProvider>(category, LogLevel.None);
    builder.Logging.AddFilter<EventSourceLoggerProvider>(category, LogLevel.None);
    builder.Logging.AddSerilog(new LoggerConfiguration()
        .MinimumLevel.Information()
        .Filter.ByIncludingOnly(Matching.FromSource<PersonalizedRecipesLog>())
        .WriteTo.File(Path.Combine(builder.Environment.ContentRootPath, "logs", "personalized-recipes-.log"),
            rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
        .CreateLogger(), dispose: true);
}

builder.Services.AddControllers();
builder.Services.AddAutoMapper(configuration => configuration.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"],
    typeof(RequestMappingProfile).Assembly, typeof(ApplicationMappingProfile).Assembly);
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
        context.ModelState.SelectMany(entry => entry.Value?.Errors.Select(error => new Error
        {
            Key = ErrorKey.BadRequest,
            Field = entry.Key,
            Message = string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value" : error.ErrorMessage
        }) ?? []).ToList());
});
builder.Services.AddSwaggerDocumentation();
builder.Services.AddSignalR();
builder.Services.AddScoped<IRecipeNotificationSender, RecipeNotificationSender>();
builder.Services.AddScoped<IGoogleAuthFlowService, GoogleAuthFlowService>();

builder.Services
    .AddInfrastructureServices(builder.Configuration, builder.Environment)
    .AddApplicationServices();

var token = builder.Configuration.GetSection("Jwt:Key").Value ?? throw new InvalidOperationException("Jwt key not found");
var issuer = builder.Configuration.GetSection("Jwt:Issuer").Value ?? throw new InvalidOperationException("Jwt issuer not found");
var audience = builder.Configuration.GetSection("Jwt:Audience").Value ?? throw new InvalidOperationException("Jwt audience not found");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/hubs/recipes")
                    && context.Request.Query.TryGetValue("access_token", out var accessToken))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new[]
                {
                    new Error { Key = ErrorKey.Incorrect, Message = "Authentication required or token invalid" }
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new[]
                {
                    new Error { Key = ErrorKey.Forbidden, Message = "Access denied" }
                });
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(token)),
            ValidIssuer = issuer,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateAudience = true,
            ValidateIssuer = true
        };
    });

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new[]
    {
        new Error { Key = ErrorKey.UnexpectedError, Message = "An unexpected error occurred." }
    });
}));

app.UseSwaggerDocumentation();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
    await DbInitializer.InitializeAsync(dbContext);
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("TestData:Enabled"))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        await TestDataInitializer.InitializeAsync(dbContext, userManager, fileService);
    }
}

app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RecipeNotificationsHub>("/hubs/recipes");

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/dev/cache/invalidate", async (IServiceProvider services) =>
    {
        var connection = services.GetService<IConnectionMultiplexer>();
        if (connection == null)
        {
            return Results.Conflict("Redis caching is disabled.");
        }

        var deleted = await DevelopmentCacheInvalidator.InvalidateAsync(connection);
        return Results.Ok(new { DeletedKeys = deleted });
    });
}

app.Run();
