using Sizzle.Application.Dtos;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface ICommentRepository : IRepository<Comment>
{
    Task<PageResponse<Comment>> GetByRecipeIdAsync(Guid recipeId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<Comment?> GetWithUserAsync(Guid commentId, CancellationToken cancellationToken = default);

    Task<string?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken = default);
}