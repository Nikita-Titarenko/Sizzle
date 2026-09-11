namespace Sizzle.WebApi.Requests.Users;

public class UpdateUserProfileRequestModel
{
    public required string Name { get; init; }

    public IFormFile? Icon { get; init; }
}
