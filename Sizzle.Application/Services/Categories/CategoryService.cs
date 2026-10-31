using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Repositories;

namespace Sizzle.Application.Services.Categories;

public class CategoryService(ICategoryRepository categoryRepository, ICategoryReadCache cache) : ICategoryService
{
    public Task<IReadOnlyList<CategoryDto>> GetAllAsync(string languageCode, CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(languageCode, token => GetAllCoreAsync(languageCode, token), cancellationToken);

    private async Task<IReadOnlyList<CategoryDto>> GetAllCoreAsync(string languageCode, CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(category => new CategoryDto
        {
            Id = category.Id,
            Name = category.Translations.FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? category.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? string.Empty
        }).OrderBy(category => category.Name).ToList();
    }
}