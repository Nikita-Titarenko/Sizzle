using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class CommentRepository(ApplicationDbContext dbContext) : Repository<Comment>(dbContext), ICommentRepository
{
    public Task<List<Comment>> GetByRecipeIdAsync(Guid recipeId)
    {
        return DbSet.AsNoTracking()
            .Where(comment => comment.RecipeId == recipeId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync();
    }
}