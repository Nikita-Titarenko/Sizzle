using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class UserFollowerRepository(ApplicationDbContext dbContext) : IUserFollowerRepository
{
    public Task<bool> UserExistsAsync(Guid userId)
    {
        return dbContext.Users.AnyAsync(user => user.Id == userId);
    }

    public Task<UserFollower?> GetAsync(Guid followerId, Guid followingId)
    {
        return dbContext.UserFollowers.FindAsync(followerId, followingId).AsTask();
    }

    public Task<List<Guid>> GetFollowerIdsAsync(Guid followingId)
    {
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowingId == followingId)
            .Select(follow => follow.FollowerId)
            .ToListAsync();
    }

    public Task<List<ApplicationUser>> GetFollowersAsync(Guid userId)
    {
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowingId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenBy(follow => follow.FollowerId)
            .Select(follow => follow.Follower)
            .ToListAsync();
    }

    public Task<List<ApplicationUser>> GetFollowingAsync(Guid userId)
    {
        return dbContext.UserFollowers.AsNoTracking()
            .Where(follow => follow.FollowerId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenBy(follow => follow.FollowingId)
            .Select(follow => follow.Following)
            .ToListAsync();
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