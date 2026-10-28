using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Dtos;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class CommentRepository(ApplicationDbContext dbContext) : Repository<Comment>(dbContext), ICommentRepository
{
    public async Task<PageResponse<Comment>> GetByRecipeIdAsync(Guid recipeId, int pageIndex, int pageSize)
    {
        var query = DbSet.AsNoTracking()
            .Where(comment => comment.RecipeId == recipeId);

        var totalSize = await query.CountAsync();
        var offset = (long)pageIndex * pageSize;
        if (offset > int.MaxValue)
        {
            return new PageResponse<Comment>
            {
                TotalSize = totalSize,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        var comments = await query
            .Include(comment => comment.User)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync();

        return new PageResponse<Comment>
        {
            Data = comments,
            TotalSize = totalSize,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public Task<Comment?> GetWithUserAsync(Guid commentId)
    {
        return DbSet.Include(comment => comment.User)
            .FirstOrDefaultAsync(comment => comment.Id == commentId);
    }

    public Task<string?> GetUserNameAsync(Guid userId)
    {
        return DbContext.Users.Where(user => user.Id == userId)
            .Select(user => user.Name)
            .FirstOrDefaultAsync();
    }
}