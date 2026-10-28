using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Users;

public interface IUserFollowerService
{
    Task<Result> FollowAsync(Guid followerId, Guid followingId);

    Task<Result> UnfollowAsync(Guid followerId, Guid followingId);
}