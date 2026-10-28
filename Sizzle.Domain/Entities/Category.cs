namespace Sizzle.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }

    public ICollection<CategoryTranslation> Translations { get; set; } = [];

    public ICollection<Recipe> Recipes { get; set; } = [];
}