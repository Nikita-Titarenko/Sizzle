using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IUserFollowerRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserFollower?> GetAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetFollowerIdsAsync(Guid followingId, CancellationToken cancellationToken = default);

    Task<int> GetFollowerCountAsync(Guid followingId, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetFollowingIdsAsync(Guid followerId, CancellationToken cancellationToken = default);

    Task<List<ApplicationUser>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<ApplicationUser>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(UserFollower follower);

    void Remove(UserFollower follower);
}