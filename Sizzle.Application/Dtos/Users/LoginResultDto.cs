namespace Sizzle.Application.Dtos.Users;

public class LoginResultDto
{
    public Guid UserId { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool IsNewUser { get; init; }

    public string? JwtToken { get; init; }
}
