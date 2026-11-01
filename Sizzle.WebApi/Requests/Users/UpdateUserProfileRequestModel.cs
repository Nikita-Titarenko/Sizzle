using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Users;

public class UpdateUserProfileRequestModel
{
    public required string Name { get; init; }

    /// <summary>Omit to keep the current description; send an empty value to clear it.</summary>
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Description { get; init; }

    public IFormFile? Icon { get; init; }
}
