using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure;
using Sizzle.Application.Services.Recipes;
using Sizzle.Application.Services;
using Sizzle.WebApi.Hubs;
using Sizzle.WebApi.ServiceExtensions;
using Sizzle.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

app.Run();
