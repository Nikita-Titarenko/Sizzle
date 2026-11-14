using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure;

internal sealed class IngredientSeed
{
    public Guid Id { get; set; }

    public bool IsBasic { get; set; }

    public Guid? ParentId { get; set; }

    public IngredientUnit Unit { get; set; }

    public int? Calories { get; set; }
}