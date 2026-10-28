using Sizzle.Application.Dtos;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface ICommentRepository : IRepository<Comment>
{
    Task<PageResponse<Comment>> GetByRecipeIdAsync(Guid recipeId, int pageIndex, int pageSize);

    Task<Comment?> GetWithUserAsync(Guid commentId);

    Task<string?> GetUserNameAsync(Guid userId);
}