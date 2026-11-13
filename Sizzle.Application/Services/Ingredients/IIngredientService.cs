using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Ingredients;

public interface IIngredientService
{
    Task<IEnumerable<IngredientListItemDto>> GetAllAsync(GetAllIngredientsDto dto, CancellationToken cancellationToken = default);

    Result<IEnumerable<IngredientProportionItemDto>> CalculateProportions(CalculateIngredientProportionsDto dto);
}
