using Microsoft.AspNetCore.Identity;

namespace Sizzle.Domain.Common;

public static class DefaultRoles
{
    public static readonly IdentityRole<Guid> UserRole = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "User",
        NormalizedName = "USER"
    };

    public static readonly IdentityRole<Guid> AdminRole = new()
    {
        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Name = "Admin",
        NormalizedName = "ADMIN"
    };
}
