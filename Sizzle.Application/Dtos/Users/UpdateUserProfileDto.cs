namespace Sizzle.Application.Dtos.Users;

public class UpdateUserProfileDto
{
    public required string Name { get; init; }

    public Stream? Icon { get; init; }

    public string? IconName { get; init; }
}
