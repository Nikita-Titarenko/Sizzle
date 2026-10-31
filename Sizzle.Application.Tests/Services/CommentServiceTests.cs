using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Comments;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class CommentServiceTests
{
    private readonly ICommentRepository comments = Substitute.For<ICommentRepository>();
    private readonly IRecipeRepository recipes = Substitute.For<IRecipeRepository>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper mapper = Substitute.For<IMapper>();
    private readonly ICommentReadCache cache = Substitute.For<ICommentReadCache>();

    private CommentService CreateService() => new(comments, recipes, unitOfWork, mapper, cache,
        NullLogger<CommentService>.Instance);

    [Fact]
    public async Task GetByRecipeIdAsync_ReturnsCachedPageWithoutAccessingRepositories()
    {
        using var source = new CancellationTokenSource();
        var query = new GetCommentsByRecipeIdDto { RecipeId = Guid.NewGuid(), PageIndex = 1, PageSize = 5 };
        var page = new PageResponse<CommentDto> { Data = [new CommentDto { Content = "Cached" }], PageIndex = 1, PageSize = 5, TotalSize = 6 };
        cache.GetOrCreateAsync(query.RecipeId, 1, 5, Arg.Any<Func<Task<PageResponse<CommentDto>?>>>())
            .Returns(page);

        var result = await CreateService().GetByRecipeIdAsync(query, source.Token);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
        await recipes.DidNotReceive().Exists(Arg.Any<CancellationToken>(), Arg.Any<object[]>());
        await comments.DidNotReceive().GetByRecipeIdAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByRecipeIdAsync_MissingRecipeReturnsNotFoundWithoutLoadingComments()
    {
        using var source = new CancellationTokenSource();
        var query = new GetCommentsByRecipeIdDto { RecipeId = Guid.NewGuid(), PageSize = 10 };
        UseCacheFactory();

        var result = await CreateService().GetByRecipeIdAsync(query, source.Token);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await recipes.Received(1).Exists(source.Token, query.RecipeId);
        await comments.DidNotReceive().GetByRecipeIdAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByRecipeIdAsync_MapsPageAndForwardsPaginationAndToken()
    {
        using var source = new CancellationTokenSource();
        var query = new GetCommentsByRecipeIdDto { RecipeId = Guid.NewGuid(), PageIndex = 2, PageSize = 3 };
        var comment = new Comment { Id = Guid.NewGuid() };
        var projected = new CommentDto { Id = comment.Id };
        UseCacheFactory();
        recipes.Exists(source.Token, query.RecipeId).Returns(true);
        comments.GetByRecipeIdAsync(query.RecipeId, 2, 3, source.Token)
            .Returns(new PageResponse<Comment> { Data = [comment], PageIndex = 2, PageSize = 3, TotalSize = 8 });
        mapper.Map<CommentDto>(comment).Returns(projected);

        var result = await CreateService().GetByRecipeIdAsync(query, source.Token);

        Assert.True(result.IsSuccess);
        Assert.Same(projected, Assert.Single(result.Value!.Data));
        Assert.Equal(8, result.Value.TotalSize);
        Assert.Equal(2, result.Value.PageIndex);
        Assert.Equal(3, result.Value.PageSize);
        await comments.Received(1).GetByRecipeIdAsync(query.RecipeId, 2, 3, source.Token);
        await cache.Received(1).GetOrCreateAsync(query.RecipeId, 2, 3, Arg.Any<Func<Task<PageResponse<CommentDto>?>>>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_BlankContentReturnsBadRequestWithoutDatabaseAccess(string content)
    {
        var input = NewComment(content);

        var result = await CreateService().CreateAsync(input);

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await recipes.DidNotReceive().Exists(Arg.Any<CancellationToken>(), Arg.Any<object[]>());
        comments.DidNotReceive().Add(Arg.Any<Comment>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ContentLongerThanLimitReturnsBadRequest()
    {
        var result = await CreateService().CreateAsync(NewComment(new string('x', 2001)));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        comments.DidNotReceive().Add(Arg.Any<Comment>());
    }

    [Fact]
    public async Task CreateAsync_MissingRecipeReturnsNotFound()
    {
        using var source = new CancellationTokenSource();
        var input = NewComment("Good recipe");

        var result = await CreateService().CreateAsync(input, source.Token);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await recipes.Received(1).Exists(source.Token, input.RecipeId);
        comments.DidNotReceive().Add(Arg.Any<Comment>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_MissingParentReturnsNotFound()
    {
        using var source = new CancellationTokenSource();
        var input = NewComment("Reply", Guid.NewGuid());
        recipes.Exists(source.Token, input.RecipeId).Returns(true);

        var result = await CreateService().CreateAsync(input, source.Token);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await comments.Received(1).GetAsync(source.Token, input.ParentId!.Value);
        comments.DidNotReceive().Add(Arg.Any<Comment>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ParentFromAnotherRecipeReturnsBadRequest()
    {
        var input = NewComment("Reply", Guid.NewGuid());
        recipes.Exists(Arg.Any<CancellationToken>(), input.RecipeId).Returns(true);
        comments.GetAsync(Arg.Any<CancellationToken>(), input.ParentId!.Value)
            .Returns(new Comment { RecipeId = Guid.NewGuid() });

        var result = await CreateService().CreateAsync(input);

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        comments.DidNotReceive().Add(Arg.Any<Comment>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ValidReplyCommitsAndAddsToCache()
    {
        using var source = new CancellationTokenSource();
        var input = NewComment("  Nice recipe  ", Guid.NewGuid());
        var entity = new Comment { RecipeId = input.RecipeId, ParentId = input.ParentId, UserId = input.UserId, Content = "Nice recipe" };
        var projected = new CommentDto { RecipeId = input.RecipeId, Content = "Nice recipe" };
        recipes.Exists(source.Token, input.RecipeId).Returns(true);
        comments.GetAsync(source.Token, input.ParentId!.Value).Returns(new Comment { RecipeId = input.RecipeId });
        mapper.Map<Comment>(input).Returns(entity);
        mapper.Map<CommentDto>(entity, Arg.Any<Action<IMappingOperationOptions<object, CommentDto>>>())
            .Returns(projected);
        comments.GetUserNameAsync(input.UserId, source.Token).Returns("Chef");
        var before = DateTime.UtcNow;

        var result = await CreateService().CreateAsync(input, source.Token);

        Assert.True(result.IsSuccess);
        Assert.Same(projected, result.Value);
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.InRange(entity.CreatedAt, before, DateTime.UtcNow);
        comments.Received(1).Add(entity);
        await unitOfWork.Received(1).CommitAsync(source.Token);
        await comments.Received(1).GetUserNameAsync(input.UserId, source.Token);
        await cache.Received(1).AddAsync(projected);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public async Task UpdateAsync_BlankContentReturnsBadRequestWithoutLoadingComment(string content)
    {
        var result = await CreateService().UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), content);

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await comments.DidNotReceive().GetWithUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ContentLongerThanLimitReturnsBadRequest()
    {
        var result = await CreateService().UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new string('x', 2001));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await comments.DidNotReceive().GetWithUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_MissingCommentReturnsNotFound()
    {
        using var source = new CancellationTokenSource();
        var id = Guid.NewGuid();

        var result = await CreateService().UpdateAsync(id, Guid.NewGuid(), "Edited", source.Token);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await comments.Received(1).GetWithUserAsync(id, source.Token);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_OtherUserReturnsForbiddenWithoutMutating()
    {
        var id = Guid.NewGuid();
        var comment = new Comment { Id = id, UserId = Guid.NewGuid(), Content = "Original" };
        comments.GetWithUserAsync(id, Arg.Any<CancellationToken>()).Returns(comment);

        var result = await CreateService().UpdateAsync(id, Guid.NewGuid(), "Edited");

        Assert.Equal(ErrorKey.Forbidden, Assert.Single(result.Errors).Key);
        Assert.Equal("Original", comment.Content);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await cache.DidNotReceive().UpdateAsync(Arg.Any<CommentDto>());
    }

    [Fact]
    public async Task UpdateAsync_AuthorTrimsContentCommitsAndUpdatesCache()
    {
        using var source = new CancellationTokenSource();
        var authorId = Guid.NewGuid();
        var comment = new Comment { Id = Guid.NewGuid(), UserId = authorId, Content = "Original" };
        var projected = new CommentDto { Id = comment.Id, Content = "Edited" };
        comments.GetWithUserAsync(comment.Id, source.Token).Returns(comment);
        mapper.Map<CommentDto>(comment).Returns(projected);

        var result = await CreateService().UpdateAsync(comment.Id, authorId, "  Edited  ", source.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("Edited", comment.Content);
        Assert.Same(projected, result.Value);
        await unitOfWork.Received(1).CommitAsync(source.Token);
        await cache.Received(1).UpdateAsync(projected);
    }

    [Fact]
    public async Task DeleteAsync_MissingCommentReturnsNotFound()
    {
        using var source = new CancellationTokenSource();
        var id = Guid.NewGuid();

        var result = await CreateService().DeleteAsync(id, Guid.NewGuid(), source.Token);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await comments.Received(1).GetAsync(source.Token, id);
        comments.DidNotReceive().Remove(Arg.Any<Comment>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_OtherUserReturnsForbidden()
    {
        var comment = new Comment { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        comments.GetAsync(Arg.Any<CancellationToken>(), comment.Id).Returns(comment);

        var result = await CreateService().DeleteAsync(comment.Id, Guid.NewGuid());

        Assert.Equal(ErrorKey.Forbidden, Assert.Single(result.Errors).Key);
        comments.DidNotReceive().Remove(Arg.Any<Comment>());
        await cache.DidNotReceive().InvalidateAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task DeleteAsync_AuthorRemovesCommitsAndInvalidatesRecipeCache()
    {
        using var source = new CancellationTokenSource();
        var comment = new Comment { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeId = Guid.NewGuid() };
        comments.GetAsync(source.Token, comment.Id).Returns(comment);

        var result = await CreateService().DeleteAsync(comment.Id, comment.UserId, source.Token);

        Assert.True(result.IsSuccess);
        comments.Received(1).Remove(comment);
        await unitOfWork.Received(1).CommitAsync(source.Token);
        await cache.Received(1).InvalidateAsync(comment.RecipeId);
    }

    private void UseCacheFactory() =>
        cache.GetOrCreateAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Func<Task<PageResponse<CommentDto>?>>>())
            .Returns(call => call.ArgAt<Func<Task<PageResponse<CommentDto>?>>>(3)());

    private static CreateCommentDto NewComment(string content, Guid? parentId = null) => new()
    {
        RecipeId = Guid.NewGuid(), UserId = Guid.NewGuid(), ParentId = parentId, Content = content
    };
}