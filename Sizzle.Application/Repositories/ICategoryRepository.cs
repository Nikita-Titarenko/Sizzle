using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface ICategoryRepository
{
    Task<bool> ExistsAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
}