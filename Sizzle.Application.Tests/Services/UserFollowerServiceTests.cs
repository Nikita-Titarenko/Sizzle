using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Recipes;
using Sizzle.Application.Services.Users;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class UserFollowerServiceTests
{
    private readonly IUserFollowerRepository repository = Substitute.For<IUserFollowerRepository>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserReadCache userCache = Substitute.For<IUserReadCache>();
    private readonly IRecipeReadCache recipeCache = Substitute.For<IRecipeReadCache>();

    private UserFollowerService CreateService() => new(repository, unitOfWork, Substitute.For<IMapper>(), userCache,
        recipeCache, NullLogger<UserFollowerService>.Instance);

    [Fact]
    public async Task FollowAsync_RejectsSelfFollowWithoutReadingRepository()
    {
        var userId = Guid.NewGuid();

        var result = await CreateService().FollowAsync(userId, userId);

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await repository.DidNotReceive().UserExistsAsync(Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().CommitAsync();
    }

    [Fact]
    public async Task FollowAsync_RejectsMissingUser()
    {
        var result = await CreateService().FollowAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        repository.DidNotReceive().Add(Arg.Any<UserFollower>());
    }

    [Fact]
    public async Task FollowAsync_RejectsDuplicateFollow()
    {
        var follower = Guid.NewGuid();
        var following = Guid.NewGuid();
        repository.UserExistsAsync(following).Returns(true);
        repository.GetAsync(follower, following).Returns(new UserFollower());

        var result = await CreateService().FollowAsync(follower, following);

        Assert.Equal(ErrorKey.AlreadyExists, Assert.Single(result.Errors).Key);
        await unitOfWork.DidNotReceive().CommitAsync();
    }

    [Fact]
    public async Task FollowAsync_CommitsAndUpdatesBothCaches()
    {
        var follower = Guid.NewGuid();
        var following = Guid.NewGuid();
        repository.UserExistsAsync(following).Returns(true);
        var before = DateTime.UtcNow;

        var result = await CreateService().FollowAsync(follower, following);

        Assert.True(result.IsSuccess);
        repository.Received(1).Add(Arg.Is<UserFollower>(follow => follow.FollowerId == follower
            && follow.FollowingId == following && follow.CreatedAt >= before));
        await unitOfWork.Received(1).CommitAsync();
        await userCache.Received(1).UpdateFollowAsync(follower, following, true);
        await recipeCache.Received(1).UpdateFollowAsync(follower, following, true);
    }

    [Fact]
    public async Task UnfollowAsync_RejectsMissingSubscription()
    {
        var result = await CreateService().UnfollowAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await unitOfWork.DidNotReceive().CommitAsync();
    }

    [Fact]
    public async Task UnfollowAsync_RemovesAndUpdatesBothCaches()
    {
        var follower = Guid.NewGuid();
        var following = Guid.NewGuid();
        var follow = new UserFollower { FollowerId = follower, FollowingId = following };
        repository.GetAsync(follower, following).Returns(follow);

        var result = await CreateService().UnfollowAsync(follower, following);

        Assert.True(result.IsSuccess);
        repository.Received(1).Remove(follow);
        await unitOfWork.Received(1).CommitAsync();
        await userCache.Received(1).UpdateFollowAsync(follower, following, false);
        await recipeCache.Received(1).UpdateFollowAsync(follower, following, false);
    }
}
