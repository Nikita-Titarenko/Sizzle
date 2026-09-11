using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Options;
using Sizzle.Infrastructure.Services;

namespace Sizzle.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("SizzleDb"));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IFileService, FileService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.Configure<SmtpEmailOptions>(configuration.GetSection("EmailOptions"));
        services.Configure<JwtTokenOptions>(configuration.GetSection("Jwt"));

        return services;
    }
}
