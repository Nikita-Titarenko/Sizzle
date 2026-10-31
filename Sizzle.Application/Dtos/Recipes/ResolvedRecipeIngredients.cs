using Sizzle.Domain.Entities;

namespace Sizzle.Application.Dtos.Recipes;

public sealed record ResolvedRecipeIngredients(Dictionary<Guid, int> Quantities, Dictionary<Guid, Ingredient> ById,
	IReadOnlyList<Ingredient> Created);