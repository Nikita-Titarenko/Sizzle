namespace Sizzle.Application.Dtos.Users;

public class UserAdminListItemDto
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public required string Role { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsBanned { get; set; }

    public DateTime? BannedUntil { get; set; }
}
