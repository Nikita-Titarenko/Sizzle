using Sizzle.Application.Dtos.Users;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Users;

public interface IUserService
{
    Task<Result<RegisterResultDto>> RegisterUserAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);

    Task<Result> ResendRegistrationEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<ConfirmEmailResultDto>> ConfirmEmailAsync(ConfirmEmailDto dto, CancellationToken cancellationToken = default);

    Task<Result<LoginResultDto>> LoginAsync(LoginUserDto dto, CancellationToken cancellationToken = default);

    Task<Result<LoginResultDto>> LoginWithGoogleAsync(GoogleUserDto profile, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> GetUserDtoAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto, CancellationToken cancellationToken = default);
}
