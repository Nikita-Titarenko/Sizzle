using Sizzle.Application.Dtos.Users;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Users;

public interface IGoogleOAuthService
{
    string GetAuthorizationUrl(string state);

    Task<Result<GoogleUserDto>> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
}