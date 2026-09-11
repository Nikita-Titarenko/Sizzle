using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Users;

public class UserService(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IJwtTokenService jwtTokenService,
    IFileService fileService) : IUserService
{
    private const string IconFolderName = "icons";

    public async Task<Result<RegisterResultDto>> RegisterUserAsync(RegisterUserDto dto)
    {
        var code = GenerateVerificationCode();
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            Name = dto.Name,
            VerificationCode = code
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var duplicate = result.Errors.Any(e => e.Code == "DuplicateUserName");
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

        _ = await userManager.AddToRoleAsync(user, DefaultRoles.UserRole.Name!);
        await SendRegistrationEmailAsync(user.Email!, code);

        return new Result<RegisterResultDto>
        {
            Value = new RegisterResultDto { UserId = user.Id }
        };
    }

    public async Task<Result> ResendRegistrationEmailAsync(Guid userId)
    {
        var getUserResult = await GetUserAsync(userId);
        if (!getUserResult.IsSuccess)
        {
            return new Result { Errors = getUserResult.Errors };
        }

        var user = getUserResult.Value!;
        if (user.EmailConfirmed)
        {
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
        _ = await userManager.UpdateAsync(user);
        await SendRegistrationEmailAsync(user.Email!, code);

        return new Result();
    }

    public async Task<Result<ConfirmEmailResultDto>> ConfirmEmailAsync(ConfirmEmailDto dto)
    {
        var getUserResult = await GetUserAsync(dto.UserId);
        if (!getUserResult.IsSuccess)
        {
            return new Result<ConfirmEmailResultDto> { Errors = getUserResult.Errors };
        }

        var user = getUserResult.Value!;
        if (user.EmailConfirmed)
        {
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
        _ = await userManager.UpdateAsync(user);

        var role = (await userManager.GetRolesAsync(user)).First();
        return new Result<ConfirmEmailResultDto>
        {
            Value = new ConfirmEmailResultDto
            {
                Role = role,
                JwtToken = jwtTokenService.GenerateToken(user.Id, role)
            }
        };
    }

    public async Task<Result> IsUserBannedAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new Result
            {
                Errors = [new Error { Key = ErrorKey.NotFound, Message = "Your account doesn't exist." }]
            };
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return new Result
            {
                Errors =
                [
                    new Error
                    {
                        Key = ErrorKey.Banned,
                        Message = "You are banned.",
                        Parameters =
                        {
                            { "BannedUntil", user.LockoutEnd.Value.ToString("O") },
                            { "Reason", user.BanReason ?? string.Empty }
                        }
                    }
                ]
            };
        }

        return new Result();
    }

    public async Task<Result<LoginResultDto>> LoginAsync(LoginUserDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return new Result<LoginResultDto> { Errors = [GetUserNotFoundError()] };
        }

        var isPasswordCorrect = await userManager.CheckPasswordAsync(user, dto.Password);
        var emailConfirmed = true;

        if (!isPasswordCorrect)
        {
            if (user.EmailConfirmed)
            {
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
            _ = await userManager.UpdateAsync(user);
            await SendRegistrationEmailAsync(dto.Email, code);
            emailConfirmed = false;
        }

        var role = (await userManager.GetRolesAsync(user)).First();
        return new Result<LoginResultDto>
        {
            Value = new LoginResultDto
            {
                UserId = user.Id,
                EmailConfirmed = emailConfirmed,
                Role = role,
                JwtToken = emailConfirmed ? jwtTokenService.GenerateToken(user.Id, role) : null
            }
        };
    }

    public async Task<Result<LoginResultDto>> HandleGoogleLoginAsync(string idToken)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
        }
        catch (InvalidJwtException)
        {
            return new Result<LoginResultDto>
            {
                Errors =
                [
                    new Error
                    {
                        Message = "Invalid Google token",
                        Field = nameof(idToken),
                        Key = ErrorKey.BadRequest
                    }
                ]
            };
        }

        var user = await userManager.FindByLoginAsync("Google", payload.Subject);
        var isNewUser = false;

        if (user == null)
        {
            user = await userManager.FindByEmailAsync(payload.Email);
            if (user == null)
            {
                isNewUser = true;
                user = new ApplicationUser
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    EmailConfirmed = true,
                    Name = payload.Name
                };

                _ = await userManager.CreateAsync(user);
                _ = await userManager.AddToRoleAsync(user, DefaultRoles.UserRole.Name!);
            }

            var info = new UserLoginInfo("Google", payload.Subject, "Google");
            _ = await userManager.AddLoginAsync(user, info);
        }

        var role = (await userManager.GetRolesAsync(user)).First();
        return new Result<LoginResultDto>
        {
            Value = new LoginResultDto
            {
                UserId = user.Id,
                Role = role,
                JwtToken = jwtTokenService.GenerateToken(user.Id, role),
                EmailConfirmed = user.EmailConfirmed,
                IsNewUser = isNewUser
            }
        };
    }

    public async Task<Result<UserDto>> GetUserDtoAsync(Guid userId)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return new Result<UserDto>
            {
                Errors = [new Error { Message = "User not found", Key = ErrorKey.NotFound }]
            };
        }

        return new Result<UserDto>
        {
            Value = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? string.Empty,
                TotalGames = user.TotalGames,
                TotalWins = user.TotalWins,
                ImageUrl = fileService.GetFileUrl(Path.Combine(IconFolderName, user.Id.ToString()))
            }
        };
    }

    public async Task<Result<IEnumerable<UserAdminListItemDto>>> GetAllUsersAsync()
    {
        var users = await userManager.Users.ToListAsync();
        var result = new List<UserAdminListItemDto>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserAdminListItemDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? string.Empty,
                ImageUrl = fileService.GetFileUrl(Path.Combine(IconFolderName, user.Id.ToString())),
                IsBanned = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                BannedUntil = user.LockoutEnd?.UtcDateTime
            });
        }

        return new Result<IEnumerable<UserAdminListItemDto>> { Value = result };
    }

    public async Task<Result> BanUserAsync(Guid userId, BanUserDto dto)
    {
        var getUserResult = await GetUserAsync(userId);
        if (!getUserResult.IsSuccess)
        {
            return getUserResult;
        }

        var user = getUserResult.Value!;
        var lockoutEndDate = DateTimeOffset.UtcNow.AddDays(dto.Days);
        var result = await userManager.SetLockoutEndDateAsync(user, lockoutEndDate);
        if (!result.Succeeded)
        {
            return new Result
            {
                Errors = [new Error { Message = "Failed to ban user", Key = ErrorKey.UnexpectedError }]
            };
        }

        user.BanReason = dto.Reason;
        _ = await userManager.UpdateAsync(user);

        return new Result();
    }

    public async Task<Result> UnbanUserAsync(Guid userId)
    {
        var getUserResult = await GetUserAsync(userId);
        if (!getUserResult.IsSuccess)
        {
            return getUserResult;
        }

        var user = getUserResult.Value!;
        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            return new Result
            {
                Errors = [new Error { Message = "Failed to unban user", Key = ErrorKey.UnexpectedError }]
            };
        }

        return new Result();
    }

    public async Task<Result> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto)
    {
        var getUserResult = await GetUserAsync(userId);
        if (!getUserResult.IsSuccess)
        {
            return getUserResult;
        }

        var user = getUserResult.Value!;
        user.Name = dto.Name;
        _ = await userManager.UpdateAsync(user);

        if (dto.Icon != null)
        {
            if (!fileService.IsValidSize(dto.Icon))
            {
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

            var extension = string.IsNullOrWhiteSpace(dto.IconName) ? ".jpg" : Path.GetExtension(dto.IconName);
            var fileName = user.Id + extension;
            var path = Path.Combine(IconFolderName, fileName);
            await fileService.SaveFile(dto.Icon, path);
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
