using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Repositories;

namespace Sizzle.Application.Services.Categories;

public class CategoryService(ICategoryRepository categoryRepository) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(string languageCode)
    {
        var categories = await categoryRepository.GetAllAsync();
        return categories.Select(category => new CategoryDto
        {
            Id = category.Id,
            Name = category.Translations.FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? category.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? string.Empty
        }).OrderBy(category => category.Name).ToList();
    }
}