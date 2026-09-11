using Sizzle.Application.Dtos.Users;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Users;

public interface IUserService
{
    Task<Result<RegisterResultDto>> RegisterUserAsync(RegisterUserDto dto);

    Task<Result> ResendRegistrationEmailAsync(Guid userId);

    Task<Result<ConfirmEmailResultDto>> ConfirmEmailAsync(ConfirmEmailDto dto);

    Task<Result<LoginResultDto>> LoginAsync(LoginUserDto dto);

    Task<Result<LoginResultDto>> HandleGoogleLoginAsync(string idToken);

    Task<Result<UserDto>> GetUserDtoAsync(Guid userId);

    Task<Result> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto);
}
