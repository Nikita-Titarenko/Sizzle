using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IIngredientRepository : IRepository<Ingredient>
{
    Task<List<Ingredient>> GetAllWithTranslationsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Ingredient>> GetAllOrderedAsync(string? search, int? pageIndex, int? pageSize, string languageCode, CancellationToken cancellationToken = default);

    Task<List<Ingredient>> GetByIdsAsync(Guid[] ids, CancellationToken cancellationToken = default);

    Task<List<Ingredient>> GetByIdsWithTranslationsAsync(Guid[] ids, CancellationToken cancellationToken = default);

    Task<List<Ingredient>> GetByIdsWithTranslationsTrackedAsync(Guid[] ids, CancellationToken cancellationToken = default);

    Task<List<Ingredient>> GetByNamesAsync(string[] normalizedNames, CancellationToken cancellationToken = default);
}
