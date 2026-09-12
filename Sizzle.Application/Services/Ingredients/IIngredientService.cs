using Sizzle.Application.Dtos.Ingredients;

namespace Sizzle.Application.Services.Ingredients;

public interface IIngredientService
{
    Task<IEnumerable<IngredientListItemDto>> GetAllAsync(GetAllIngredientsDto dto);
}
