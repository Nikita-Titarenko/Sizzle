using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface ICategoryRepository
{
    Task<bool> ExistsAsync(Guid categoryId);

    Task<List<Category>> GetAllAsync();
}