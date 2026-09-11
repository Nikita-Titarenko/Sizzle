namespace Sizzle.Application.Dtos.Users;

public class UserDto
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public string? ImageUrl { get; set; }
}
