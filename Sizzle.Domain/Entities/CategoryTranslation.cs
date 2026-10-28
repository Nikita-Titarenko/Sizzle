namespace Sizzle.Domain.Entities;

public class CategoryTranslation
{
    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public string LanguageCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}