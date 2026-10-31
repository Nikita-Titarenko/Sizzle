using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Users;

public class GoogleIdTokenRequestModel
{
    [Required]
    public required string IdToken { get; init; }
}
