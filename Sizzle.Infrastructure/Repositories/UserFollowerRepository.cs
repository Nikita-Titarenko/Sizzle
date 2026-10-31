using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class UserFollowerRepository(ApplicationDbContext dbContext) : IUserFollowerRepository
{
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken);
    }

    public Task<UserFollower?> GetAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserFollowers.FindAsync([followerId, followingId], cancellationToken).AsTask();
    }

    public Task<List<Guid>> GetFollowerIdsAsync(Guid followingId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowingId == followingId)
            .Select(follow => follow.FollowerId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ApplicationUser>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowingId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenBy(follow => follow.FollowerId)
            .Select(follow => follow.Follower)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ApplicationUser>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowerId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenBy(follow => follow.FollowingId)
            .Select(follow => follow.Following)
            .ToListAsync(cancellationToken);
    }

    public void Add(UserFollower follower)
    {
        dbContext.UserFollowers.Add(follower);
    }

    public void Remove(UserFollower follower)
    {
        dbContext.UserFollowers.Remove(follower);
    }
}