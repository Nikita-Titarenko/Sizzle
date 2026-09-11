namespace Sizzle.WebApi.Requests.Users;

public class EmailConfirmationRequestModel
{
    public Guid UserId { get; init; }

    public required string Code { get; init; }
}
