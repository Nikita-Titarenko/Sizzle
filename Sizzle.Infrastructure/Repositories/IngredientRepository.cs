using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Extensions;

namespace Sizzle.Infrastructure.Repositories;

public class IngredientRepository(ApplicationDbContext dbContext, ILogger<IngredientRepository>? logger = null) : Repository<Ingredient>(dbContext), IIngredientRepository
{
    private readonly ILogger<IngredientRepository> logger = logger ?? NullLogger<IngredientRepository>.Instance;

    public Task<List<Ingredient>> GetAllWithTranslationsAsync(CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().Include(ingredient => ingredient.Translations).ToListAsync(cancellationToken);
    }

    public Task<List<Ingredient>> GetByIdsAsync(Guid[] ids, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.ingredients.get_by_ids");
        return DbSet.WhereIdIn(ids, ingredient => ingredient.Id).ToListAsync(cancellationToken);
    }

    public Task<List<Ingredient>> GetByIdsWithTranslationsAsync(Guid[] ids, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.ingredients.get_by_ids_with_translations");
        return DbSet.AsNoTracking().WhereIdIn(ids, ingredient => ingredient.Id)
            .Include(ingredient => ingredient.Translations).ToListAsync(cancellationToken);
    }

    public Task<List<Ingredient>> GetByNamesAsync(string[] normalizedNames, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.ingredients.get_by_names");
        return DbSet.Include(i => i.Translations)
            .Where(i => i.Translations.Any(t => normalizedNames.Contains(t.Name.ToLower())))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Ingredient>> GetAllOrderedAsync(string? search, int? pageIndex, int? pageSize, string languageCode, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.ingredients.get_all_ordered");
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

        return await query.Include(i => i.Translations).ToListAsync(cancellationToken);
    }
}
