using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Dtos;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class CommentRepository(ApplicationDbContext dbContext, ILogger<CommentRepository>? logger = null) : Repository<Comment>(dbContext), ICommentRepository
{
    private readonly ILogger<CommentRepository> logger = logger ?? NullLogger<CommentRepository>.Instance;

    public async Task<PageResponse<Comment>> GetByRecipeIdAsync(Guid recipeId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.comments.get_by_recipe_page");
        var query = DbSet.AsNoTracking()
            .Where(comment => comment.RecipeId == recipeId);

        var totalSize = await query.CountAsync(cancellationToken);
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
            .ToListAsync(cancellationToken);

        return new PageResponse<Comment>
        {
            Data = comments,
            TotalSize = totalSize,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public Task<Comment?> GetWithUserAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.comments.get_with_user");
        return DbSet.Include(comment => comment.User)
            .FirstOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);
    }

    public Task<string?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.users.get_name");
        return DbContext.Users.Where(user => user.Id == userId)
            .Select(user => user.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }
}