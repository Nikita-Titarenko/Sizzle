namespace Sizzle.WebApi.Requests.Users;

public class LoginRequestModel
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}
