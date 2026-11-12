using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class CategoryRepository(ApplicationDbContext dbContext, ILogger<CategoryRepository>? logger = null) : ICategoryRepository
{
    private readonly ILogger<CategoryRepository> logger = logger ?? NullLogger<CategoryRepository>.Instance;

    public Task<bool> ExistsAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.categories.exists");
        return dbContext.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken);
    }

    public Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.categories.get_all_with_translations");
        return dbContext.Categories.AsNoTracking().Include(category => category.Translations).ToListAsync(cancellationToken);
    }
}