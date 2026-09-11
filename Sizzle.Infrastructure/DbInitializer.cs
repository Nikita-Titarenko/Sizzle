using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sizzle.Domain.Common;

namespace Sizzle.Infrastructure;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        if (!await roleManager.Roles.AnyAsync(r => r.Name == DefaultRoles.UserRole.Name))
        {
            _ = await roleManager.CreateAsync(DefaultRoles.UserRole);
        }

        if (!await roleManager.Roles.AnyAsync(r => r.Name == DefaultRoles.AdminRole.Name))
        {
            _ = await roleManager.CreateAsync(DefaultRoles.AdminRole);
        }
    }
}
