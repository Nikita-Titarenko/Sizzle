using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IIngredientRepository : IRepository<Ingredient>
{
    Task<IEnumerable<Ingredient>> GetAllOrderedAsync(string? search, int? pageIndex, int? pageSize, string languageCode);

    Task<List<Ingredient>> GetByIdsAsync(Guid[] ids);

    Task<List<Ingredient>> GetByNamesAsync(string[] normalizedNames);
}
