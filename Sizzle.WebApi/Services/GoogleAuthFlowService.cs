using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Services.Users;
using Sizzle.Domain.Common;

namespace Sizzle.WebApi.Services;

public interface IGoogleAuthFlowService
{
    IActionResult Start(HttpContext context);

    Task<IActionResult> CompleteAsync(HttpContext context, string? code, string? state, string? error,
        CancellationToken cancellationToken);
}

public sealed class GoogleAuthFlowService(
    IGoogleOAuthService googleOAuthService,
    IUserService userService,
    IDataProtectionProvider dataProtectionProvider) : IGoogleAuthFlowService
{
    private const string StateCookie = "sizzle.google.state";
    private const string CallbackPath = "/api/Users/google-callback";
    private const string ProtectorPurpose = "Sizzle.GoogleOAuth.State";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(5);

    public IActionResult Start(HttpContext context)
    {
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var url = googleOAuthService.GetAuthorizationUrl(state);
        var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        context.Response.Cookies.Append(StateCookie, protector.Protect(state, StateLifetime), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = CallbackPath,
            MaxAge = StateLifetime
        });

        return new RedirectResult(url);
    }

    public async Task<IActionResult> CompleteAsync(HttpContext context, string? code, string? state, string? error,
        CancellationToken cancellationToken)
    {
        context.Request.Cookies.TryGetValue(StateCookie, out var protectedState);
        context.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = CallbackPath });

        string? expectedState = null;
        if (!string.IsNullOrEmpty(protectedState))
        {
            try
            {
                expectedState = dataProtectionProvider.CreateProtector(ProtectorPurpose)
                    .ToTimeLimitedDataProtector().Unprotect(protectedState);
            }
            catch (CryptographicException)
            {
                return InvalidRequest("Invalid Google authorization state");
            }
        }

        if (state == null || expectedState == null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(expectedState)))
        {
            return InvalidRequest("Invalid Google authorization state");
        }

        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
        {
            return InvalidRequest("Google authorization was denied");
        }

        var profile = await googleOAuthService.ExchangeCodeAsync(code, cancellationToken);
        if (!profile.IsSuccess)
        {
            return Failure(profile);
        }

        var result = await userService.LoginWithGoogleAsync(profile.Value!);
        return result.IsSuccess ? new OkObjectResult(result.Value) : Failure(result);
    }

    private static IActionResult InvalidRequest(string message) => new BadRequestObjectResult(new[]
    {
        new Error { Key = ErrorKey.BadRequest, Message = message }
    });

    private static IActionResult Failure(Result result) => result.Errors.FirstOrDefault()?.Key switch
    {
        ErrorKey.NotFound => new NotFoundObjectResult(result.Errors),
        ErrorKey.AlreadyExists => new ConflictObjectResult(result.Errors),
        ErrorKey.Forbidden => new ObjectResult(result.Errors) { StatusCode = StatusCodes.Status403Forbidden },
        _ => new BadRequestObjectResult(result.Errors)
    };
}