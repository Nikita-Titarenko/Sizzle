using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface ICommentRepository : IRepository<Comment>
{
    Task<List<Comment>> GetByRecipeIdAsync(Guid recipeId);
}