using Sizzle.Application.Dtos.Users;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Users;

public interface IGoogleOAuthService
{
    string GetAuthorizationUrl(string state);

    Task<Result<GoogleUserDto>> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Verifies an ID token from native Google sign-in (e.g. Android Credential Manager)
    /// that was requested for this server's client ID.</summary>
    Task<Result<GoogleUserDto>> ValidateIdTokenAsync(string idToken);
}