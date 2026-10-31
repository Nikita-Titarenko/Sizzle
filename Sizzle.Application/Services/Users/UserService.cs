using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Users;

public class UserService(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IJwtTokenService jwtTokenService,
    IFileService fileService,
    IUserReadCache userCache,
    IMapper mapper,
    ILogger<UserService> logger) : IUserService
{
    private const string IconFolderName = "icons";

    public async Task<Result<RegisterResultDto>> RegisterUserAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var code = GenerateVerificationCode();
        var user = mapper.Map<ApplicationUser>(dto);
        user.VerificationCode = code;

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var duplicate = result.Errors.Any(e => e.Code == "DuplicateUserName");
            logger.LogWarning("User registration failed: {Reason}", duplicate ? "duplicate username" : "identity creation failed");
            return new Result<RegisterResultDto>
            {
                Errors =
                [
                    new Error
                    {
                        Message = duplicate ? "User with this email already exists" : "Failed to register user",
                        Field = "Email",
                        Key = duplicate ? ErrorKey.AlreadyExists : ErrorKey.UnexpectedError
                    }
                ]
            };
        }

        logger.LogInformation("User {UserId} registered", user.Id);
        await SendRegistrationEmailAsync(user.Email!, code);

        return new Result<RegisterResultDto>
        {
            Value = new RegisterResultDto { UserId = user.Id }
        };
    }

    public async Task<Result> ResendRegistrationEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var getUserResult = await GetUserAsync(userId);
        if (!getUserResult.IsSuccess)
        {
            logger.LogWarning("Confirmation resend rejected: user {UserId} not found", userId);
            return new Result { Errors = getUserResult.Errors };
        }

        var user = getUserResult.Value!;
        if (user.EmailConfirmed)
        {
            logger.LogWarning("Confirmation resend rejected: user {UserId} already confirmed", userId);
            return new Result
            {
                Errors =
                [
                    new Error
                    {
                        Message = "Email already confirmed",
                        Field = "Email",
                        Key = ErrorKey.AlreadyConfirmed
                    }
                ]
            };
        }

        var code = GenerateVerificationCode();
        user.VerificationCode = code;
        var updateResult = await userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            logger.LogInformation("Verification code updated for user {UserId}", userId);
        }
        else
        {
            logger.LogError("Verification code update failed for user {UserId}: {ErrorCodes}", userId,
                string.Join(", ", updateResult.Errors.Select(error => error.Code)));
        }
        await SendRegistrationEmailAsync(user.Email!, code);

        return new Result();
    }

    public async Task<Result<ConfirmEmailResultDto>> ConfirmEmailAsync(ConfirmEmailDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var getUserResult = await GetUserAsync(dto.UserId);
        if (!getUserResult.IsSuccess)
        {
            logger.LogWarning("Email confirmation rejected: user {UserId} not found", dto.UserId);
            return new Result<ConfirmEmailResultDto> { Errors = getUserResult.Errors };
        }

        var user = getUserResult.Value!;
        if (user.EmailConfirmed)
        {
            logger.LogWarning("Email confirmation rejected: user {UserId} already confirmed", dto.UserId);
            return new Result<ConfirmEmailResultDto>
            {
                Errors =
                [
                    new Error
                    {
                        Message = "Email already confirmed",
                        Field = "Email",
                        Key = ErrorKey.AlreadyConfirmed
                    }
                ]
            };
        }

        if (user.VerificationCode != dto.Code)
        {
            logger.LogWarning("Email confirmation rejected: invalid code for user {UserId}", dto.UserId);
            return new Result<ConfirmEmailResultDto>
            {
                Errors =
                [
                    new Error
                    {
                        Message = "Token incorrect",
                        Field = "Email",
                        Key = ErrorKey.Incorrect
                    }
                ]
            };
        }

        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            logger.LogInformation("Email confirmed for user {UserId}", user.Id);
        }
        else
        {
            logger.LogError("Email confirmation update failed for user {UserId}: {ErrorCodes}", user.Id,
                string.Join(", ", updateResult.Errors.Select(error => error.Code)));
        }

        return new Result<ConfirmEmailResultDto>
        {
            Value = new ConfirmEmailResultDto
            {
                JwtToken = jwtTokenService.GenerateToken(user.Id)
            }
        };
    }

    public async Task<Result<LoginResultDto>> LoginAsync(LoginUserDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            logger.LogWarning("Login rejected: user not found");
            return new Result<LoginResultDto> { Errors = [GetUserNotFoundError()] };
        }

        var isPasswordCorrect = await userManager.CheckPasswordAsync(user, dto.Password);

        if (!isPasswordCorrect)
        {
            if (user.EmailConfirmed)
            {
                logger.LogWarning("Login rejected: invalid password for user {UserId}", user.Id);
                return new Result<LoginResultDto>
                {
                    Errors =
                    [
                        new Error
                        {
                            Message = "Login or password incorrect",
                            Field = string.Empty,
                            Key = ErrorKey.IncorrectLoginOrPassword
                        }
                    ]
                };
            }

            var code = GenerateVerificationCode();
            user.VerificationCode = code;
            var updateResult = await userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                logger.LogInformation("Verification code updated after login for user {UserId}", user.Id);
            }
            else
            {
                logger.LogError("Verification code update failed after login for user {UserId}: {ErrorCodes}", user.Id,
                    string.Join(", ", updateResult.Errors.Select(error => error.Code)));
            }
            await SendRegistrationEmailAsync(dto.Email, code);
        }

        return new Result<LoginResultDto>
        {
            Value = new LoginResultDto
            {
                UserId = user.Id,
                EmailConfirmed = user.EmailConfirmed,
                JwtToken = user.EmailConfirmed ? jwtTokenService.GenerateToken(user.Id) : null
            }
        };
    }

    public async Task<Result<LoginResultDto>> LoginWithGoogleAsync(GoogleUserDto profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByLoginAsync("Google", profile.Subject);
        var isNewUser = false;

        if (user == null)
        {
            user = await userManager.FindByEmailAsync(profile.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = profile.Email,
                    Email = profile.Email,
                    EmailConfirmed = true,
                    Name = profile.Name
                };

                var created = await userManager.CreateAsync(user);
                if (!created.Succeeded)
                {
                    logger.LogWarning("Google registration failed");
                    return GoogleLoginFailed();
                }

                isNewUser = true;
                logger.LogInformation("User {UserId} created through Google login", user.Id);
            }

            var linked = await userManager.AddLoginAsync(user, new UserLoginInfo("Google", profile.Subject, "Google"));
            if (!linked.Succeeded)
            {
                logger.LogWarning("Google account linking failed for user {UserId}", user.Id);
                return GoogleLoginFailed();
            }
            logger.LogInformation("Google account linked for user {UserId}", user.Id);
        }

        return new Result<LoginResultDto>
        {
            Value = new LoginResultDto
            {
                UserId = user.Id,
                JwtToken = jwtTokenService.GenerateToken(user.Id),
                EmailConfirmed = user.EmailConfirmed,
                IsNewUser = isNewUser
            }
        };
    }

    private static Result<LoginResultDto> GoogleLoginFailed() => new()
    {
        Errors = [new Error { Key = ErrorKey.UnexpectedError, Message = "Google login failed" }]
    };

    public async Task<Result<UserDto>> GetUserDtoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userCache.GetProfileAsync(userId, async () =>
        {
            var profile = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            return profile == null ? null : mapper.Map<UserDto>(profile);
        });
        return user == null
            ? new Result<UserDto> { Errors = [new Error { Message = "User not found", Key = ErrorKey.NotFound }] }
            : new Result<UserDto> { Value = user };
    }

    public async Task<Result> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var getUserResult = await GetUserAsync(userId);
        if (!getUserResult.IsSuccess)
        {
            logger.LogWarning("Profile update rejected: user {UserId} not found", userId);
            return getUserResult;
        }

        var user = getUserResult.Value!;
        if (dto.Icon != null && !fileService.IsValidSize(dto.Icon))
        {
            logger.LogWarning("Profile update rejected: icon too large for user {UserId}", userId);
            return new Result
            {
                Errors =
                [
                    new Error
                    {
                        Message = "File size exceeds the limit",
                        Field = "Icon",
                        Key = ErrorKey.BadRequest
                    }
                ]
            };
        }

        user.Name = dto.Name;
        if (dto.Description != null)
        {
            user.Description = dto.Description;
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            logger.LogError("Profile update failed for user {UserId}: {ErrorCodes}", userId,
                string.Join(", ", updateResult.Errors.Select(error => error.Code)));
        }

        if (dto.Icon != null)
        {
            var extension = string.IsNullOrWhiteSpace(dto.IconName) ? ".jpg" : Path.GetExtension(dto.IconName);
            var fileName = user.Id + extension;
            var path = Path.Combine(IconFolderName, fileName);
            await fileService.SaveFile(dto.Icon, path);
        }

        await userCache.UpdateProfileAsync(mapper.Map<UserDto>(user));
        if (updateResult.Succeeded)
        {
            logger.LogInformation("Profile updated for user {UserId}", userId);
        }
        return new Result();
    }

    private async Task<Result<ApplicationUser>> GetUserAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user == null
            ? new Result<ApplicationUser> { Errors = [GetUserNotFoundError()] }
            : new Result<ApplicationUser> { Value = user };
    }

    private static Error GetUserNotFoundError()
    {
        return new Error
        {
            Message = "User not found",
            Field = string.Empty,
            Key = ErrorKey.NotFound
        };
    }

    private static string GenerateVerificationCode()
    {
        var random = new Random();
        return random.Next(1000000).ToString("D6");
    }

    private async Task SendRegistrationEmailAsync(string email, string code)
    {
        await emailSender.SendEmailAsync(
            email,
            "Registration confirmation",
            $"Confirm email to register in Sizzle. Confirmation code: {code}");
    }
}
