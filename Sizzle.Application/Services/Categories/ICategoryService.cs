using Sizzle.Application.Dtos.Categories;

namespace Sizzle.Application.Services.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(string languageCode);
}