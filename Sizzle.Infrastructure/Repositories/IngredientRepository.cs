using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class IngredientRepository(ApplicationDbContext dbContext) : Repository<Ingredient>(dbContext), IIngredientRepository
{
    public Task<List<Ingredient>> GetByIdsAsync(Guid[] ids)
    {
        return DbSet.Where(i => ids.Contains(i.Id)).ToListAsync();
    }

    public Task<List<Ingredient>> GetByNamesAsync(string[] normalizedNames)
    {
        return DbSet.Include(i => i.Translations)
            .Where(i => i.Translations.Any(t => normalizedNames.Contains(t.Name.ToLower())))
            .ToListAsync();
    }

    public async Task<IEnumerable<Ingredient>> GetAllOrderedAsync(string? search, int? pageIndex, int? pageSize, string languageCode)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(i =>
                (i.Translations.Where(t => t.LanguageCode == languageCode).Select(t => t.Name).FirstOrDefault()
                    ?? i.Translations.Where(t => t.LanguageCode == "en").Select(t => t.Name).FirstOrDefault()!)
                .Contains(normalizedSearch));
        }

        query = query.OrderBy(i => i.Translations.Where(t => t.LanguageCode == languageCode).Select(t => t.Name).FirstOrDefault()
            ?? i.Translations.Where(t => t.LanguageCode == "en").Select(t => t.Name).FirstOrDefault());

        if (pageIndex.HasValue && pageSize.HasValue && pageIndex.Value >= 0 && pageSize.Value > 0)
        {
            query = query
                .Skip(pageIndex.Value * pageSize.Value)
                .Take(pageSize.Value);
        }

        return await query.Include(i => i.Translations).ToListAsync();
    }
}
