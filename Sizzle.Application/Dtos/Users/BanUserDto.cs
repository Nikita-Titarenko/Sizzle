namespace Sizzle.Application.Dtos.Users;

public class BanUserDto
{
    public int Days { get; init; }

    public required string Reason { get; init; }
}
