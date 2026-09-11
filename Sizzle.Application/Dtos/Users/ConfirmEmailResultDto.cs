namespace Sizzle.Application.Dtos.Users;

public class ConfirmEmailResultDto
{
    public required string Role { get; init; }

    public required string JwtToken { get; init; }
}
