using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Repositories;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class UserFollowerRepositoryTests : InMemoryRepositoryTest
{
    [Fact]
    public async Task GetFollowersAndFollowingAsync_UseCorrectDirectionAndNewestFirst()
    {
        var owner = new ApplicationUser { Id = Guid.NewGuid(), Name = "Owner" };
        var early = new ApplicationUser { Id = Guid.NewGuid(), Name = "Early" };
        var recent = new ApplicationUser { Id = Guid.NewGuid(), Name = "Recent" };
        var followed = new ApplicationUser { Id = Guid.NewGuid(), Name = "Followed" };
        await using (var seed = CreateContext())
        {
            seed.Users.AddRange(owner, early, recent, followed);
            seed.UserFollowers.AddRange(
                new UserFollower { FollowerId = early.Id, FollowingId = owner.Id, CreatedAt = DateTime.UnixEpoch },
                new UserFollower { FollowerId = recent.Id, FollowingId = owner.Id, CreatedAt = DateTime.UnixEpoch.AddDays(1) },
                new UserFollower { FollowerId = owner.Id, FollowingId = followed.Id, CreatedAt = DateTime.UnixEpoch });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new UserFollowerRepository(db);

        Assert.Equal([recent.Id, early.Id], (await repository.GetFollowersAsync(owner.Id)).Select(user => user.Id));
        Assert.Equal([followed.Id], (await repository.GetFollowingAsync(owner.Id)).Select(user => user.Id));
        var followerIds = await repository.GetFollowerIdsAsync(owner.Id);
        Assert.Equal(2, followerIds.Count);
        Assert.Contains(recent.Id, followerIds);
        Assert.Contains(early.Id, followerIds);
        Assert.Equal(2, await repository.GetFollowerCountAsync(owner.Id));
        Assert.Equal([followed.Id], await repository.GetFollowingIdsAsync(owner.Id));
        Assert.NotNull(await repository.GetAsync(recent.Id, owner.Id));
        Assert.Null(await repository.GetAsync(owner.Id, recent.Id));
        Assert.True(await repository.UserExistsAsync(owner.Id));
        Assert.False(await repository.UserExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddAndRemove_PersistFollow()
    {
        var follower = Guid.NewGuid();
        var following = Guid.NewGuid();
        await using var db = CreateContext();
        var repository = new UserFollowerRepository(db);
        var follow = new UserFollower { FollowerId = follower, FollowingId = following };

        repository.Add(follow);
        await db.SaveChangesAsync();
        Assert.NotNull(await repository.GetAsync(follower, following));

        repository.Remove(follow);
        await db.SaveChangesAsync();
        Assert.Null(await repository.GetAsync(follower, following));
    }
}
