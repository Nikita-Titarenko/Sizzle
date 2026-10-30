using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Users;

public class UserFollowerService(IUserFollowerRepository followerRepository, IUnitOfWork unitOfWork,
    IFileService fileService) : IUserFollowerService
{
    public async Task<List<UserDto>> GetFollowersAsync(Guid userId)
    {
        var users = await followerRepository.GetFollowersAsync(userId);
        return users.Select(ToUserDto).ToList();
    }

    public async Task<List<UserDto>> GetFollowingAsync(Guid userId)
    {
        var users = await followerRepository.GetFollowingAsync(userId);
        return users.Select(ToUserDto).ToList();
    }

    private UserDto ToUserDto(ApplicationUser user)
    {
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            ImageUrl = fileService.GetFileUrl(Path.Combine("icons", user.Id.ToString()))
        };
    }

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