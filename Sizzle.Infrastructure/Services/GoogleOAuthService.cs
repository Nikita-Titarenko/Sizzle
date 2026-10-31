using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services.Users;
using Sizzle.Domain.Common;
using Sizzle.Infrastructure.Options;

namespace Sizzle.Infrastructure.Services;

public sealed class GoogleOAuthService(HttpClient httpClient, IOptions<GoogleOAuthOptions> options) : IGoogleOAuthService
{
    private readonly GoogleOAuthOptions _options = options.Value;

    public string GetAuthorizationUrl(string state)
    {
        ValidateOptions();
        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state
        });
    }

    public async Task<Result<GoogleUserDto>> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        ValidateOptions();
        if (string.IsNullOrWhiteSpace(code))
        {
            return InvalidGoogleLogin();
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });
        using var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return InvalidGoogleLogin();
        }

        try
        {
            using var tokenJson = await JsonDocument.ParseAsync(
                await tokenResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!tokenJson.RootElement.TryGetProperty("access_token", out var accessTokenElement)
                || !tokenJson.RootElement.TryGetProperty("id_token", out var idTokenElement)
                || accessTokenElement.GetString() is not { Length: > 0 } accessToken
                || idTokenElement.GetString() is not { Length: > 0 } idToken)
            {
                return InvalidGoogleLogin();
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_options.ClientId]
            });

            using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
            userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var userInfoResponse = await httpClient.SendAsync(userInfoRequest, cancellationToken);
            if (!userInfoResponse.IsSuccessStatusCode)
            {
                return InvalidGoogleLogin();
            }

            using var userInfo = await JsonDocument.ParseAsync(
                await userInfoResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = userInfo.RootElement;
            if (!root.TryGetProperty("sub", out var subjectElement)
                || !root.TryGetProperty("email", out var emailElement)
                || !root.TryGetProperty("email_verified", out var verifiedElement)
                || verifiedElement.ValueKind != JsonValueKind.True
                || subjectElement.GetString() is not { Length: > 0 } subject
                || emailElement.GetString() is not { Length: > 0 } email
                || !string.Equals(subject, payload.Subject, StringComparison.Ordinal)
                || !string.Equals(email, payload.Email, StringComparison.OrdinalIgnoreCase))
            {
                return InvalidGoogleLogin();
            }

            var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            return new Result<GoogleUserDto> { Value = new GoogleUserDto(subject, email, name ?? email) };
        }
        catch (Exception exception) when (exception is JsonException or InvalidJwtException or InvalidOperationException)
        {
            return InvalidGoogleLogin();
        }
    }

    public async Task<Result<GoogleUserDto>> ValidateIdTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("GoogleOAuth:ClientId must be configured.");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_options.ClientId]
            });
            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return InvalidGoogleLogin();
            }

            return new Result<GoogleUserDto>
            {
                Value = new GoogleUserDto(payload.Subject, payload.Email, payload.Name ?? payload.Email)
            };
        }
        catch (InvalidJwtException)
        {
            return InvalidGoogleLogin();
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || !Uri.TryCreate(_options.RedirectUri, UriKind.Absolute, out var redirectUri)
            || (redirectUri.Scheme != Uri.UriSchemeHttps && !(redirectUri.Scheme == Uri.UriSchemeHttp && redirectUri.IsLoopback)))
        {
            throw new InvalidOperationException("GoogleOAuth:ClientId, ClientSecret and an HTTPS RedirectUri must be configured.");
        }
    }

    private static Result<GoogleUserDto> InvalidGoogleLogin() => new()
    {
        Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Google authorization failed" }]
    };
}