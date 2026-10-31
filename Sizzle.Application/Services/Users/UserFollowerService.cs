using AutoMapper;
using Microsoft.Extensions.Logging;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Users;

public class UserFollowerService(IUserFollowerRepository followerRepository, IUnitOfWork unitOfWork,
    IMapper mapper, IUserReadCache userCache, IRecipeReadCache recipeCache,
    ILogger<UserFollowerService> logger) : IUserFollowerService
{
    public async Task<List<UserDto>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await userCache.GetFollowersAsync(userId, async () =>
            (await followerRepository.GetFollowersAsync(userId, cancellationToken)).Select(mapper.Map<UserDto>).ToList());
    }

    public async Task<List<UserDto>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await userCache.GetFollowingAsync(userId, async () =>
            (await followerRepository.GetFollowingAsync(userId, cancellationToken)).Select(mapper.Map<UserDto>).ToList());
    }

    public async Task<Result> FollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        if (followerId == followingId)
        {
            logger.LogWarning("Follow rejected: user {UserId} attempted to follow themselves", followerId);
            return new Result { Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Cannot follow yourself" }] };
        }

        if (!await followerRepository.UserExistsAsync(followingId, cancellationToken))
        {
            logger.LogWarning("Follow rejected: user {FollowingId} not found", followingId);
            return new Result { Errors = [new Error { Key = ErrorKey.NotFound, Message = "User not found" }] };
        }

        if (await followerRepository.GetAsync(followerId, followingId, cancellationToken) != null)
        {
            logger.LogWarning("Follow rejected: user {FollowerId} already follows {FollowingId}", followerId, followingId);
            return new Result { Errors = [new Error { Key = ErrorKey.AlreadyExists, Message = "Already following this user" }] };
        }

        followerRepository.Add(new UserFollower
        {
            FollowerId = followerId,
            FollowingId = followingId,
            CreatedAt = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("User {FollowerId} followed {FollowingId}", followerId, followingId);
        await userCache.UpdateFollowAsync(followerId, followingId, true);
        await recipeCache.UpdateFollowAsync(followerId, followingId, true);
        return new Result();
    }

    public async Task<Result> UnfollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        var follow = await followerRepository.GetAsync(followerId, followingId, cancellationToken);
        if (follow == null)
        {
            logger.LogWarning("Unfollow rejected: user {FollowerId} does not follow {FollowingId}", followerId, followingId);
            return new Result { Errors = [new Error { Key = ErrorKey.NotFound, Message = "Subscription not found" }] };
        }

        followerRepository.Remove(follow);
        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("User {FollowerId} unfollowed {FollowingId}", followerId, followingId);
        await userCache.UpdateFollowAsync(followerId, followingId, false);
        await recipeCache.UpdateFollowAsync(followerId, followingId, false);
        return new Result();
    }
}