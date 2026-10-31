using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Ingredients;

public interface IIngredientReadCache
{
    Task<List<IngredientListItemDto>> GetOrCreateAsync(GetAllIngredientsDto query,
        Func<Task<List<IngredientListItemDto>>> factory);

    Task IngredientsCreatedAsync(IReadOnlyCollection<Ingredient> ingredients);
}
