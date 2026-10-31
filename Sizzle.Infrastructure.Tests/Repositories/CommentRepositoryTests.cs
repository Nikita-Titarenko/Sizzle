using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Repositories;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class CommentRepositoryTests : InMemoryRepositoryTest
{
    [Fact]
    public async Task GetByRecipeIdAsync_FiltersOrdersPagesAndIncludesUser()
    {
        var recipeId = Guid.NewGuid();
        var otherRecipeId = Guid.NewGuid();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Name = "Author" };
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await using (var seed = CreateContext())
        {
            seed.Users.Add(user);
            seed.Comments.AddRange(
                new Comment { Id = secondId, RecipeId = recipeId, UserId = user.Id, CreatedAt = DateTime.UnixEpoch },
                new Comment { Id = firstId, RecipeId = recipeId, UserId = user.Id, CreatedAt = DateTime.UnixEpoch },
                new Comment { Id = Guid.NewGuid(), RecipeId = recipeId, UserId = user.Id, CreatedAt = DateTime.UnixEpoch.AddDays(1) },
                new Comment { Id = Guid.NewGuid(), RecipeId = otherRecipeId, UserId = user.Id });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var page = await new CommentRepository(db).GetByRecipeIdAsync(recipeId, 1, 1);

        Assert.Equal(3, page.TotalSize);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal(1, page.PageSize);
        var comment = Assert.Single(page.Data);
        Assert.Equal(secondId, comment.Id);
        Assert.Equal("Author", comment.User.Name);
    }

    [Fact]
    public async Task GetByRecipeIdAsync_ReturnsEmptyPageWhenOffsetOverflows()
    {
        var recipeId = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            seed.Comments.Add(new Comment { Id = Guid.NewGuid(), RecipeId = recipeId, UserId = Guid.NewGuid() });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var page = await new CommentRepository(db).GetByRecipeIdAsync(recipeId, int.MaxValue, 2);

        Assert.Equal(1, page.TotalSize);
        Assert.Empty(page.Data);
    }

    [Fact]
    public async Task GetWithUserAsync_AndGetUserNameAsync_ReadAuthor()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Name = "Writer" };
        var commentId = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            seed.Users.Add(user);
            seed.Comments.Add(new Comment { Id = commentId, UserId = user.Id, RecipeId = Guid.NewGuid() });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new CommentRepository(db);
        var comment = await repository.GetWithUserAsync(commentId);

        Assert.NotNull(comment);
        Assert.Equal("Writer", comment.User.Name);
        Assert.Equal("Writer", await repository.GetUserNameAsync(user.Id));
        Assert.Null(await repository.GetUserNameAsync(Guid.NewGuid()));
    }
}
