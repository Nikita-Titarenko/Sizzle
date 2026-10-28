using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IUserFollowerRepository
{
    Task<bool> UserExistsAsync(Guid userId);

    Task<UserFollower?> GetAsync(Guid followerId, Guid followingId);

    Task<List<Guid>> GetFollowerIdsAsync(Guid followingId);

    void Add(UserFollower follower);

    void Remove(UserFollower follower);
}