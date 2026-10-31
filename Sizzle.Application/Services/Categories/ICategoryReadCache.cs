using Sizzle.Application.Dtos.Categories;

namespace Sizzle.Application.Services.Categories;

public interface ICategoryReadCache
{
    Task<IReadOnlyList<CategoryDto>> GetOrCreateAsync(string languageCode,
        Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>> factory, CancellationToken cancellationToken = default);
}