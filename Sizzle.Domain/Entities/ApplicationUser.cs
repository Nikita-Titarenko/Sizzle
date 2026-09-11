using Microsoft.AspNetCore.Identity;

namespace Sizzle.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string? VerificationCode { get; set; }

    public string? BanReason { get; set; }

    public int TotalGames { get; set; }

    public int TotalWins { get; set; }
}
