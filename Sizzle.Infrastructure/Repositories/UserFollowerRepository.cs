using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class UserFollowerRepository(ApplicationDbContext dbContext, ILogger<UserFollowerRepository>? logger = null) : IUserFollowerRepository
{
    private readonly ILogger<UserFollowerRepository> logger = logger ?? NullLogger<UserFollowerRepository>.Instance;

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.user_followers.user_exists");
        return dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken);
    }

    public Task<UserFollower?> GetAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.user_followers.get");
        return dbContext.UserFollowers.FindAsync([followerId, followingId], cancellationToken).AsTask();
    }

    public Task<List<Guid>> GetFollowerIdsAsync(Guid followingId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.user_followers.get_follower_ids");
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowingId == followingId)
            .Select(follow => follow.FollowerId)
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetFollowerCountAsync(Guid followingId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.user_followers.get_follower_count");
        return dbContext.UserFollowers.AsNoTracking()
            .CountAsync(follow => follow.FollowingId == followingId, cancellationToken);
    }

    public Task<List<Guid>> GetFollowingIdsAsync(Guid followerId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.user_followers.get_following_ids");
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowerId == followerId)
            .Select(follow => follow.FollowingId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ApplicationUser>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.user_followers.get_followers");
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowingId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenBy(follow => follow.FollowerId)
            .Select(follow => follow.Follower)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ApplicationUser>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.user_followers.get_following");
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowerId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenBy(follow => follow.FollowingId)
            .Select(follow => follow.Following)
            .ToListAsync(cancellationToken);
    }

    public void Add(UserFollower follower)
    {
        logger.LogInformation("db.user_followers.add");
        dbContext.UserFollowers.Add(follower);
    }

    public void Remove(UserFollower follower)
    {
        logger.LogInformation("db.user_followers.remove");
        dbContext.UserFollowers.Remove(follower);
    }
}