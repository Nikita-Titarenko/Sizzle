using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services.Devices;
using Sizzle.Application.Services.Users;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Devices;
using Sizzle.WebApi.Requests.Users;
using Sizzle.WebApi.Services;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class UsersController(IUserService userService, IUserFollowerService followerService,
    IGoogleAuthFlowService googleAuthFlowService, IGoogleOAuthService googleOAuthService,
    IDeviceTokenService deviceTokenService) : BaseController
{
    /// <summary>Follow a user.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — the user tries to follow themselves.</para>
    /// <para>ErrorKey.NotFound — the target user does not exist.</para>
    /// <para>ErrorKey.AlreadyExists — the subscription already exists.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost("{userId:guid}/follow")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Follow(Guid userId)
    {
        var result = await followerService.FollowAsync(GetUserId(), userId);
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    /// <summary>Unfollow a user.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the subscription does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpDelete("{userId:guid}/follow")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Unfollow(Guid userId)
    {
        var result = await followerService.UnfollowAsync(GetUserId(), userId);
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    [HttpGet("followers")]
    [Authorize]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<UserDto>>> GetFollowers()
    {
        return Ok(await followerService.GetFollowersAsync(GetUserId()));
    }

    [HttpGet("following")]
    [Authorize]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<UserDto>>> GetFollowing()
    {
        return Ok(await followerService.GetFollowingAsync(GetUserId()));
    }

    /// <summary>Register a user and send a confirmation email.</summary>
    /// <remarks>
    /// <para>ErrorKey.AlreadyExists — the email is already registered.</para>
    /// <para>ErrorKey.UnexpectedError — registration failed (400).</para>
    /// <para>ErrorKey.BadRequest — invalid request data (400).</para>
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResultDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResultDto>> RegisterUser(RegisterUserRequestModel request)
    {
        var result = await userService.RegisterUserAsync(new RegisterUserDto
        {
            Email = request.Email,
            Name = request.Name,
            Password = request.Password
        });

        if (!result.IsSuccess)
        {
            return HandleErrors(result);
        }

        return Accepted(result.Value);
    }

    /// <summary>Confirm an email address using a code and receive a token.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the user does not exist.</para>
    /// <para>ErrorKey.AlreadyConfirmed — the email is already confirmed (400).</para>
    /// <para>ErrorKey.Incorrect — the confirmation code is incorrect (400).</para>
    /// <para>ErrorKey.BadRequest — invalid request data (400).</para>
    /// </remarks>
    [HttpPost("confirm-email")]
    [ProducesResponseType(typeof(ConfirmEmailResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfirmEmailResultDto>> ConfirmEmail(EmailConfirmationRequestModel request)
    {
        var result = await userService.ConfirmEmailAsync(new ConfirmEmailDto
        {
            UserId = request.UserId,
            Code = request.Code
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Resend the email confirmation code.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the user does not exist.</para>
    /// <para>ErrorKey.AlreadyConfirmed — the email is already confirmed (400).</para>
    /// <para>ErrorKey.BadRequest — invalid request data (400).</para>
    /// </remarks>
    [HttpPost("resend-confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResendConfirmEmail(ResendEmailConfirmationRequestModel request)
    {
        var result = await userService.ResendRegistrationEmailAsync(request.UserId);
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    /// <summary>Sign in with an email address and password.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — no user exists with this email address.</para>
    /// <para>ErrorKey.IncorrectLoginOrPassword — an incorrect password for a confirmed user (400).</para>
    /// <para>ErrorKey.BadRequest — invalid request data (400).</para>
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoginResultDto>> Login(LoginRequestModel request)
    {
        var result = await userService.LoginAsync(new LoginUserDto
        {
            Email = request.Email,
            Password = request.Password
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Start Google sign-in by redirecting the browser to Google's authorization page.</summary>
    /// <remarks>Creates a short-lived state cookie to validate the Google callback.</remarks>
    [HttpGet("google-login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleLogin() => googleAuthFlowService.Start(HttpContext);

    /// <summary>Complete Google sign-in and return an application JWT.</summary>
    /// <remarks>
    /// <para>Google redirects the browser here with an authorization code and state. The code is exchanged for Google tokens, and the verified Google profile is used to sign in or register the user.</para>
    /// <para>ErrorKey.BadRequest — the state is missing, invalid or expired, authorization was denied, or Google authentication failed.</para>
    /// </remarks>
    [HttpGet("google-callback")]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GoogleCallback(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error,
        CancellationToken cancellationToken)
        => googleAuthFlowService.CompleteAsync(HttpContext, code, state, error, cancellationToken);

    /// <summary>Sign in with a Google ID token from a native app (Android Credential Manager) and return an application JWT.</summary>
    /// <remarks>
    /// <para>The ID token must be issued for this server's Google client ID (GoogleOAuth:ClientId).</para>
    /// <para>ErrorKey.BadRequest — the token is invalid, expired, issued for another client, or the email is not verified.</para>
    /// </remarks>
    [HttpPost("google-token")]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResultDto>> GoogleToken(GoogleIdTokenRequestModel request)
    {
        var profile = await googleOAuthService.ValidateIdTokenAsync(request.IdToken);
        if (!profile.IsSuccess)
        {
            return HandleErrors(profile);
        }

        var result = await userService.LoginWithGoogleAsync(profile.Value!);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Register this device's push notification token (Firebase Cloud Messaging) for the current user.</summary>
    /// <remarks>
    /// <para>Calling it again with the same token is safe; a token registered by another account moves to this one.</para>
    /// <para>ErrorKey.BadRequest — the token is empty or too long.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost("devices")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> RegisterDevice(DeviceTokenRequestModel request)
    {
        var result = await deviceTokenService.RegisterAsync(GetUserId(), request.Token);
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    /// <summary>Stop push notifications to this device (call before signing out).</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the token is not registered for the current user.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpDelete("devices")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UnregisterDevice([FromBody] DeviceTokenRequestModel request)
    {
        var result = await deviceTokenService.UnregisterAsync(GetUserId(), request.Token);
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    /// <summary>Get a user's profile.</summary>
    /// <remarks>ErrorKey.NotFound — the user does not exist.</remarks>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(Guid userId)
    {
        var result = await userService.GetUserDtoAsync(userId);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Update the current user's name and profile image.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the current user does not exist.</para>
    /// <para>ErrorKey.BadRequest — the image exceeds the size limit or the form is invalid.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPut]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateUserProfile([FromForm] UpdateUserProfileRequestModel request)
    {
        await using var stream = request.Icon?.OpenReadStream();
        var result = await userService.UpdateUserProfileAsync(GetUserId(), new UpdateUserProfileDto
        {
            Name = request.Name,
            Icon = stream,
            IconName = request.Icon?.FileName
        });

        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }
}
