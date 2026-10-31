using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services;
using Sizzle.Application.Services.Users;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class UserServiceTests
{
    private readonly UserManager<ApplicationUser> users = CreateUserManager();
    private readonly IEmailSender emails = Substitute.For<IEmailSender>();
    private readonly IJwtTokenService tokens = Substitute.For<IJwtTokenService>();
    private readonly IFileService files = Substitute.For<IFileService>();
    private readonly IUserReadCache cache = Substitute.For<IUserReadCache>();
    private readonly IMapper mapper = Substitute.For<IMapper>();

    private UserService CreateService() => new(users, emails, tokens, files, cache, mapper,
        NullLogger<UserService>.Instance);

    [Fact]
    public async Task RegisterUserAsync_SendsVerificationCodeAfterCreatingUser()
    {
        var input = new RegisterUserDto { Email = "chef@example.test", Name = "Chef", Password = "password" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = input.Email };
        mapper.Map<ApplicationUser>(input).Returns(user);
        users.CreateAsync(user, input.Password).Returns(IdentityResult.Success);

        var result = await CreateService().RegisterUserAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value!.UserId);
        Assert.Matches("^[0-9]{6}$", user.VerificationCode!);
        await emails.Received(1).SendEmailAsync(input.Email, "Registration confirmation",
            Arg.Is<string>(body => body.Contains(user.VerificationCode!)));
    }

    [Theory]
    [InlineData("DuplicateUserName", ErrorKey.AlreadyExists)]
    [InlineData("InvalidPassword", ErrorKey.UnexpectedError)]
    public async Task RegisterUserAsync_IdentityFailureDoesNotSendEmail(string identityError, ErrorKey expected)
    {
        var input = new RegisterUserDto { Email = "chef@example.test", Name = "Chef", Password = "password" };
        var user = new ApplicationUser { Email = input.Email };
        mapper.Map<ApplicationUser>(input).Returns(user);
        users.CreateAsync(user, input.Password).Returns(IdentityResult.Failed(new IdentityError { Code = identityError }));

        var result = await CreateService().RegisterUserAsync(input);

        Assert.Equal(expected, Assert.Single(result.Errors).Key);
        await emails.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResendRegistrationEmailAsync_MissingUserReturnsNotFound()
    {
        var id = Guid.NewGuid();

        var result = await CreateService().ResendRegistrationEmailAsync(id);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await users.Received(1).FindByIdAsync(id.ToString());
        await users.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
        await emails.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResendRegistrationEmailAsync_ConfirmedUserDoesNotUpdateCode()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), EmailConfirmed = true };
        users.FindByIdAsync(user.Id.ToString()).Returns(user);

        var result = await CreateService().ResendRegistrationEmailAsync(user.Id);

        Assert.Equal(ErrorKey.AlreadyConfirmed, Assert.Single(result.Errors).Key);
        await users.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task ResendRegistrationEmailAsync_UnconfirmedUserUpdatesCodeAndEmails()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "chef@example.test", VerificationCode = "old" };
        users.FindByIdAsync(user.Id.ToString()).Returns(user);
        users.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await CreateService().ResendRegistrationEmailAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Matches("^[0-9]{6}$", user.VerificationCode!);
        await users.Received(1).UpdateAsync(user);
        await emails.Received(1).SendEmailAsync(user.Email, "Registration confirmation",
            Arg.Is<string>(body => body.Contains(user.VerificationCode!)));
    }

    [Fact]
    public async Task ConfirmEmailAsync_MissingUserReturnsNotFound()
    {
        var result = await CreateService().ConfirmEmailAsync(new ConfirmEmailDto { UserId = Guid.NewGuid(), Code = "123456" });

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ConfirmEmailAsync_AlreadyConfirmedReturnsError()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), EmailConfirmed = true };
        users.FindByIdAsync(user.Id.ToString()).Returns(user);

        var result = await CreateService().ConfirmEmailAsync(new ConfirmEmailDto { UserId = user.Id, Code = "123456" });

        Assert.Equal(ErrorKey.AlreadyConfirmed, Assert.Single(result.Errors).Key);
        await users.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task ConfirmEmailAsync_WrongCodeDoesNotConfirmOrIssueToken()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), VerificationCode = "123456" };
        users.FindByIdAsync(user.Id.ToString()).Returns(user);

        var result = await CreateService().ConfirmEmailAsync(new ConfirmEmailDto { UserId = user.Id, Code = "654321" });

        Assert.Equal(ErrorKey.Incorrect, Assert.Single(result.Errors).Key);
        Assert.False(user.EmailConfirmed);
        await users.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ConfirmEmailAsync_ValidCodeConfirmsUserAndReturnsToken()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), VerificationCode = "123456" };
        users.FindByIdAsync(user.Id.ToString()).Returns(user);
        users.UpdateAsync(user).Returns(IdentityResult.Success);
        tokens.GenerateToken(user.Id).Returns("jwt");

        var result = await CreateService().ConfirmEmailAsync(new ConfirmEmailDto { UserId = user.Id, Code = "123456" });

        Assert.True(result.IsSuccess);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("jwt", result.Value!.JwtToken);
        await users.Received(1).UpdateAsync(user);
        tokens.Received(1).GenerateToken(user.Id);
    }

    [Fact]
    public async Task LoginAsync_MissingUserReturnsNotFound()
    {
        var input = new LoginUserDto { Email = "missing@example.test", Password = "password" };

        var result = await CreateService().LoginAsync(input);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await users.Received(1).FindByEmailAsync(input.Email);
        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordForConfirmedUserIsRejected()
    {
        var input = new LoginUserDto { Email = "chef@example.test", Password = "wrong" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), EmailConfirmed = true };
        users.FindByEmailAsync(input.Email).Returns(user);
        users.CheckPasswordAsync(user, input.Password).Returns(false);

        var result = await CreateService().LoginAsync(input);

        Assert.Equal(ErrorKey.IncorrectLoginOrPassword, Assert.Single(result.Errors).Key);
        await emails.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task LoginAsync_UnconfirmedUserWithWrongPasswordResendsCodeWithoutToken()
    {
        var input = new LoginUserDto { Email = "chef@example.test", Password = "wrong" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = input.Email };
        users.FindByEmailAsync(input.Email).Returns(user);
        users.CheckPasswordAsync(user, input.Password).Returns(false);
        users.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await CreateService().LoginAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value!.UserId);
        Assert.False(result.Value.EmailConfirmed);
        Assert.Null(result.Value.JwtToken);
        Assert.Matches("^[0-9]{6}$", user.VerificationCode!);
        await users.Received(1).UpdateAsync(user);
        await emails.Received(1).SendEmailAsync(input.Email, "Registration confirmation",
            Arg.Is<string>(body => body.Contains(user.VerificationCode!)));
        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task LoginAsync_ConfirmedUserWithValidPasswordGetsToken()
    {
        var input = new LoginUserDto { Email = "chef@example.test", Password = "correct" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), EmailConfirmed = true };
        users.FindByEmailAsync(input.Email).Returns(user);
        users.CheckPasswordAsync(user, input.Password).Returns(true);
        tokens.GenerateToken(user.Id).Returns("jwt");

        var result = await CreateService().LoginAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal("jwt", result.Value!.JwtToken);
        Assert.True(result.Value.EmailConfirmed);
        await users.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task LoginWithGoogleAsync_ExistingLinkedAccountReturnsTokenWithoutCreatingUser()
    {
        var profile = new GoogleUserDto("google-subject", "chef@example.test", "Chef");
        var user = new ApplicationUser { Id = Guid.NewGuid(), EmailConfirmed = true };
        users.FindByLoginAsync("Google", profile.Subject).Returns(user);
        tokens.GenerateToken(user.Id).Returns("jwt");

        var result = await CreateService().LoginWithGoogleAsync(profile);

        Assert.True(result.IsSuccess);
        Assert.Equal("jwt", result.Value!.JwtToken);
        Assert.False(result.Value.IsNewUser);
        await users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
        await users.DidNotReceive().AddLoginAsync(Arg.Any<ApplicationUser>(), Arg.Any<UserLoginInfo>());
    }

    [Fact]
    public async Task LoginWithGoogleAsync_ExistingEmailLinksGoogleAccount()
    {
        var profile = new GoogleUserDto("google-subject", "chef@example.test", "Chef");
        var user = new ApplicationUser { Id = Guid.NewGuid(), EmailConfirmed = true };
        users.FindByEmailAsync(profile.Email).Returns(user);
        users.AddLoginAsync(user, Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        var result = await CreateService().LoginWithGoogleAsync(profile);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value!.UserId);
        Assert.False(result.Value.IsNewUser);
        await users.Received(1).AddLoginAsync(user, Arg.Is<UserLoginInfo>(login =>
            login.LoginProvider == "Google" && login.ProviderKey == profile.Subject));
        await users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task LoginWithGoogleAsync_NewEmailCreatesConfirmedUserAndLinksAccount()
    {
        var profile = new GoogleUserDto("google-subject", "chef@example.test", "Chef");
        users.CreateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
        users.AddLoginAsync(Arg.Any<ApplicationUser>(), Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        var result = await CreateService().LoginWithGoogleAsync(profile);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsNewUser);
        await users.Received(1).CreateAsync(Arg.Is<ApplicationUser>(user => user.Email == profile.Email
            && user.UserName == profile.Email && user.Name == profile.Name && user.EmailConfirmed));
        await users.Received(1).AddLoginAsync(Arg.Any<ApplicationUser>(), Arg.Is<UserLoginInfo>(login =>
            login.LoginProvider == "Google" && login.ProviderKey == profile.Subject));
    }

    [Fact]
    public async Task LoginWithGoogleAsync_CreateFailureDoesNotLinkOrIssueToken()
    {
        users.CreateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Failed(new IdentityError { Code = "Failed" }));

        var result = await CreateService().LoginWithGoogleAsync(new GoogleUserDto("subject", "chef@example.test", "Chef"));

        Assert.Equal(ErrorKey.UnexpectedError, Assert.Single(result.Errors).Key);
        await users.DidNotReceive().AddLoginAsync(Arg.Any<ApplicationUser>(), Arg.Any<UserLoginInfo>());
        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task LoginWithGoogleAsync_LinkFailureDoesNotIssueToken()
    {
        var profile = new GoogleUserDto("google-subject", "chef@example.test", "Chef");
        users.FindByEmailAsync(profile.Email).Returns(new ApplicationUser { Id = Guid.NewGuid() });
        users.AddLoginAsync(Arg.Any<ApplicationUser>(), Arg.Any<UserLoginInfo>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Failed" }));

        var result = await CreateService().LoginWithGoogleAsync(profile);

        Assert.Equal(ErrorKey.UnexpectedError, Assert.Single(result.Errors).Key);
        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetUserDtoAsync_ReturnsCachedProfileWithoutMapping()
    {
        var id = Guid.NewGuid();
        var profile = new UserDto { Id = id, Name = "Chef", Email = "chef@example.test" };
        cache.GetProfileAsync(id, Arg.Any<Func<Task<UserDto?>>>()).Returns(profile);

        var result = await CreateService().GetUserDtoAsync(id);

        Assert.Same(profile, result.Value);
        Assert.True(result.IsSuccess);
        mapper.DidNotReceive().Map<UserDto>(Arg.Any<object>());
    }

    [Fact]
    public async Task GetUserDtoAsync_MissingProfileReturnsNotFound()
    {
        var id = Guid.NewGuid();

        var result = await CreateService().GetUserDtoAsync(id);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await cache.Received(1).GetProfileAsync(id, Arg.Any<Func<Task<UserDto?>>>());
    }

    [Fact]
    public async Task UpdateUserProfileAsync_MissingUserReturnsNotFoundWithoutSaving()
    {
        var id = Guid.NewGuid();

        var result = await CreateService().UpdateUserProfileAsync(id, new UpdateUserProfileDto { Name = "Chef" });

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await users.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
        await cache.DidNotReceive().UpdateProfileAsync(Arg.Any<UserDto>());
    }

    [Fact]
    public async Task UpdateUserProfileAsync_OversizedIconReturnsBadRequestWithoutUpdatingUser()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Name = "Original" };
        using var icon = new MemoryStream();
        users.FindByIdAsync(user.Id.ToString()).Returns(user);
        files.IsValidSize(icon).Returns(false);

        var result = await CreateService().UpdateUserProfileAsync(user.Id,
            new UpdateUserProfileDto { Name = "Changed", Icon = icon });

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        Assert.Equal("Original", user.Name);
        await users.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
        await cache.DidNotReceive().UpdateProfileAsync(Arg.Any<UserDto>());
    }

    [Fact]
    public async Task UpdateUserProfileAsync_UpdatesUserSavesIconAndRefreshesCache()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Description = "Original" };
        var profile = new UserDto { Id = user.Id, Name = "Chef", Email = "chef@example.test" };
        using var icon = new MemoryStream();
        users.FindByIdAsync(user.Id.ToString()).Returns(user);
        users.UpdateAsync(user).Returns(IdentityResult.Success);
        files.IsValidSize(icon).Returns(true);
        mapper.Map<UserDto>(user).Returns(profile);

        var result = await CreateService().UpdateUserProfileAsync(user.Id,
            new UpdateUserProfileDto { Name = "Chef", Description = "Updated", Icon = icon, IconName = "photo.png" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Chef", user.Name);
        Assert.Equal("Updated", user.Description);
        await users.Received(1).UpdateAsync(user);
        await files.Received(1).SaveFile(icon, Path.Combine("icons", user.Id + ".png"));
        await cache.Received(1).UpdateProfileAsync(profile);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithoutIconPreservesDescriptionAndDoesNotSaveFile()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Description = "Original" };
        users.FindByIdAsync(user.Id.ToString()).Returns(user);
        users.UpdateAsync(user).Returns(IdentityResult.Success);
        mapper.Map<UserDto>(user).Returns(new UserDto { Id = user.Id, Name = "Chef", Email = "chef@example.test" });

        var result = await CreateService().UpdateUserProfileAsync(user.Id, new UpdateUserProfileDto { Name = "Chef" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Original", user.Description);
        await files.DidNotReceive().SaveFile(Arg.Any<Stream>(), Arg.Any<string>());
        await cache.Received(1).UpdateProfileAsync(Arg.Any<UserDto>());
    }

    [Fact]
    public async Task RegisterUserAsync_CanceledTokenSkipsIdentityAndEmail()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().RegisterUserAsync(
            new RegisterUserDto { Email = "chef@example.test", Name = "Chef", Password = "password" }, source.Token));

        await users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
        await emails.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    private static UserManager<ApplicationUser> CreateUserManager() => Substitute.For<UserManager<ApplicationUser>>(
        Substitute.For<IUserStore<ApplicationUser>>(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        Substitute.For<IPasswordHasher<ApplicationUser>>(),
        Array.Empty<IUserValidator<ApplicationUser>>(),
        Array.Empty<IPasswordValidator<ApplicationUser>>(),
        Substitute.For<ILookupNormalizer>(),
        new IdentityErrorDescriber(),
        Substitute.For<IServiceProvider>(),
        NullLogger<UserManager<ApplicationUser>>.Instance);
}