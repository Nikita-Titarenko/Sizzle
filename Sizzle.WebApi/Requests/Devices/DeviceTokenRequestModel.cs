using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Devices;

public class DeviceTokenRequestModel
{
    [Required]
    [StringLength(512, MinimumLength = 1)]
    public required string Token { get; init; }
}
