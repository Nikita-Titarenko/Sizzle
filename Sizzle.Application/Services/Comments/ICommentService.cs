using Sizzle.Application.Dtos.Comments;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Comments;

public interface ICommentService
{
    Task<Result<IReadOnlyList<CommentDto>>> GetByRecipeIdAsync(Guid recipeId);

    Task<Result<CommentDto>> CreateAsync(CreateCommentDto dto);

    Task<Result<CommentDto>> UpdateAsync(Guid commentId, Guid userId, string content);

    Task<Result> DeleteAsync(Guid commentId, Guid userId);
}