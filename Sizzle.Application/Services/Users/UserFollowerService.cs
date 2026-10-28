using Sizzle.Application.Repositories;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Users;

public class UserFollowerService(IUserFollowerRepository followerRepository, IUnitOfWork unitOfWork) : IUserFollowerService
{
    public async Task<Result> FollowAsync(Guid followerId, Guid followingId)
    {
        if (followerId == followingId)
        {
            return new Result { Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Cannot follow yourself" }] };
        }

        if (!await followerRepository.UserExistsAsync(followingId))
        {
            return new Result { Errors = [new Error { Key = ErrorKey.NotFound, Message = "User not found" }] };
        }

        if (await followerRepository.GetAsync(followerId, followingId) != null)
        {
            return new Result { Errors = [new Error { Key = ErrorKey.AlreadyExists, Message = "Already following this user" }] };
        }

        followerRepository.Add(new UserFollower
        {
            FollowerId = followerId,
            FollowingId = followingId,
            CreatedAt = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();
        return new Result();
    }

    public async Task<Result> UnfollowAsync(Guid followerId, Guid followingId)
    {
        var follow = await followerRepository.GetAsync(followerId, followingId);
        if (follow == null)
        {
            return new Result { Errors = [new Error { Key = ErrorKey.NotFound, Message = "Subscription not found" }] };
        }

        followerRepository.Remove(follow);
        await unitOfWork.CommitAsync();
        return new Result();
    }
}