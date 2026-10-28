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

    public void Add(UserFollower follower)
    {
        dbContext.UserFollowers.Add(follower);
    }

    public void Remove(UserFollower follower)
    {
        dbContext.UserFollowers.Remove(follower);
    }
}