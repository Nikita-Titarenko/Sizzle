namespace Sizzle.Application.Dtos.Users;

public class RegisterUserDto
{
    public required string Email { get; init; }

    public required string Name { get; init; }

    public required string Password { get; init; }
}
