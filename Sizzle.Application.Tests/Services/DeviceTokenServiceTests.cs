using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Devices;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class DeviceTokenServiceTests
{
    private readonly IDeviceTokenRepository repository = Substitute.For<IDeviceTokenRepository>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

    private DeviceTokenService CreateService() => new(repository, unitOfWork, NullLogger<DeviceTokenService>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_RejectsEmptyTokensWithoutWriting(string token)
    {
        var result = await CreateService().RegisterAsync(Guid.NewGuid(), token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        repository.DidNotReceive().Add(Arg.Any<DeviceToken>());
        await unitOfWork.DidNotReceive().CommitAsync();
    }

    [Fact]
    public async Task RegisterAsync_RejectsOversizedToken()
    {
        var result = await CreateService().RegisterAsync(Guid.NewGuid(), new string('x', DeviceTokenService.MaxTokenLength + 1));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await unitOfWork.DidNotReceive().CommitAsync();
    }

    [Fact]
    public async Task RegisterAsync_AddsNewTokenForUser()
    {
        var userId = Guid.NewGuid();

        var result = await CreateService().RegisterAsync(userId, "new-token");

        repository.Received(1).Add(Arg.Is<DeviceToken>(value => value.Token == "new-token" && value.UserId == userId));
        Assert.True(result.IsSuccess);
        await unitOfWork.Received(1).CommitAsync();
    }

    [Fact]
    public async Task RegisterAsync_ReassignsExistingToken()
    {
        var device = new DeviceToken { Token = "shared", UserId = Guid.NewGuid() };
        var newUser = Guid.NewGuid();
        repository.GetAsync("shared").Returns(device);
        var before = DateTime.UtcNow;

        var result = await CreateService().RegisterAsync(newUser, "shared");

        Assert.True(result.IsSuccess);
        Assert.Equal(newUser, device.UserId);
        Assert.InRange(device.UpdatedAt, before, DateTime.UtcNow);
        repository.DidNotReceive().Add(Arg.Any<DeviceToken>());
        await unitOfWork.Received(1).CommitAsync();
    }

    [Fact]
    public async Task UnregisterAsync_RejectsAnotherUsersToken()
    {
        repository.GetAsync("shared").Returns(new DeviceToken { Token = "shared", UserId = Guid.NewGuid() });

        var result = await CreateService().UnregisterAsync(Guid.NewGuid(), "shared");

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        repository.DidNotReceive().Remove(Arg.Any<DeviceToken>());
        await unitOfWork.DidNotReceive().CommitAsync();
    }

    [Fact]
    public async Task UnregisterAsync_RemovesOwnedToken()
    {
        var userId = Guid.NewGuid();
        var device = new DeviceToken { Token = "owned", UserId = userId };
        repository.GetAsync("owned").Returns(device);

        var result = await CreateService().UnregisterAsync(userId, "owned");

        Assert.True(result.IsSuccess);
        repository.Received(1).Remove(device);
        await unitOfWork.Received(1).CommitAsync();
    }
}
