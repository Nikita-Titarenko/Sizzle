using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Comments;

namespace Sizzle.Application.Services.Comments;

public interface ICommentReadCache
{
    Task<PageResponse<CommentDto>?> GetOrCreateAsync(Guid recipeId, int pageIndex, int pageSize,
        Func<Task<PageResponse<CommentDto>?>> factory);
    Task AddAsync(CommentDto comment);
    Task UpdateAsync(CommentDto comment);
    Task InvalidateAsync(Guid recipeId);
}
