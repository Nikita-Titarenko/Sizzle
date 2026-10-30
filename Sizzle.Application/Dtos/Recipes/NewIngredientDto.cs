using Sizzle.Domain.Entities;

namespace Sizzle.Application.Dtos.Recipes;

public class NewIngredientDto
{
    public required string Name { get; init; }

    public Guid? ParentId { get; init; }

    public IngredientUnit Unit { get; init; }

    public int Quantity { get; init; }
}
