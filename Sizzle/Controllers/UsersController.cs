using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services.Users;
using Sizzle.WebApi.Requests.Users;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class UsersController(IUserService userService) : BaseController
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResultDto), StatusCodes.Status202Accepted)]
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

    [HttpPost("confirm-email")]
    [ProducesResponseType(typeof(ConfirmEmailResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfirmEmailResultDto>> ConfirmEmail(EmailConfirmationRequestModel request)
    {
        var result = await userService.ConfirmEmailAsync(new ConfirmEmailDto
        {
            UserId = request.UserId,
            Code = request.Code
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost("resend-confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ResendConfirmEmail(ResendEmailConfirmationRequestModel request)
    {
        var result = await userService.ResendRegistrationEmailAsync(request.UserId);
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResultDto>> Login(LoginRequestModel request)
    {
        var result = await userService.LoginAsync(new LoginUserDto
        {
            Email = request.Email,
            Password = request.Password
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost("google-login")]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResultDto>> GoogleLogin(GoogleLoginRequestModel requestModel)
    {
        var result = await userService.HandleGoogleLoginAsync(requestModel.IdToken);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> GetUser(Guid userId)
    {
        var result = await userService.GetUserDtoAsync(userId);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<UserAdminListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserAdminListItemDto>>> GetAllUsers()
    {
        var result = await userService.GetAllUsersAsync();
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost("{userId:guid}/ban")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> BanUser(Guid userId, BanUserRequestModel request)
    {
        var result = await userService.BanUserAsync(userId, new BanUserDto
        {
            Days = request.Days,
            Reason = request.Reason
        });

        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    [HttpPost("{userId:guid}/unban")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UnbanUser(Guid userId)
    {
        var result = await userService.UnbanUserAsync(userId);
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    [HttpPut]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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
