namespace Sizzle.Application.Dtos.Users;

public class ConfirmEmailDto
{
    public Guid UserId { get; init; }

    public required string Code { get; init; }
}
