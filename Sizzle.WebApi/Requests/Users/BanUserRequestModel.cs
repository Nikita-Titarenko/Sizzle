namespace Sizzle.WebApi.Requests.Users;

public class BanUserRequestModel
{
    public int Days { get; init; }

    public required string Reason { get; init; }
}
