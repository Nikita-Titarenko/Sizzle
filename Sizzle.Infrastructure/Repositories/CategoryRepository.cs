using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class CategoryRepository(ApplicationDbContext dbContext) : ICategoryRepository
{
    public Task<bool> ExistsAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return dbContext.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken);
    }

    public Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Categories.AsNoTracking().Include(category => category.Translations).ToListAsync(cancellationToken);
    }
}