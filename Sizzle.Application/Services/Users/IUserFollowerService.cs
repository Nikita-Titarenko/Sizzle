using Sizzle.Application.Dtos.Users;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Users;

public interface IUserFollowerService
{
    Task<Result> FollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);

    Task<Result> UnfollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);

    Task<List<UserDto>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<UserDto>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default);
}