namespace Sizzle.WebApi.Requests.Users;

public class RegisterUserRequestModel
{
    public required string Email { get; init; }

    public required string Name { get; init; }

    public required string Password { get; init; }
}
