using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Comments;

public interface ICommentService
{
    Task<Result<PageResponse<CommentDto>>> GetByRecipeIdAsync(GetCommentsByRecipeIdDto dto, CancellationToken cancellationToken = default);

    Task<Result<CommentDto>> CreateAsync(CreateCommentDto dto, CancellationToken cancellationToken = default);

    Task<Result<CommentDto>> UpdateAsync(Guid commentId, Guid userId, string content, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default);
}