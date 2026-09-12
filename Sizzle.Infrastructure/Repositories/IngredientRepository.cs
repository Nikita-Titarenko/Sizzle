using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class IngredientRepository(ApplicationDbContext dbContext) : Repository<Ingredient>(dbContext), IIngredientRepository
{
    public async Task<IEnumerable<Ingredient>> GetAllOrderedAsync(string? search, int? pageIndex, int? pageSize)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(i => i.Name.Contains(normalizedSearch));
        }

        query = query.OrderBy(i => i.Name);

        if (pageIndex.HasValue && pageSize.HasValue && pageIndex.Value >= 0 && pageSize.Value > 0)
        {
            query = query
                .Skip(pageIndex.Value * pageSize.Value)
                .Take(pageSize.Value);
        }

        return await query.ToListAsync();
    }
}
