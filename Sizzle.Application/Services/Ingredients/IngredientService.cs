using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Repositories;

namespace Sizzle.Application.Services.Ingredients;

public class IngredientService(IIngredientRepository ingredientRepository) : IIngredientService
{
    public async Task<IEnumerable<IngredientListItemDto>> GetAllAsync(GetAllIngredientsDto dto)
    {
        var ingredients = (await ingredientRepository.GetAllOrderedAsync(dto.Search, dto.PageIndex, dto.PageSize)).ToList();

        var allItems = ingredients.ToDictionary(
            ingredient => ingredient.Id,
            ingredient => new IngredientListItemDto
            {
                Id = ingredient.Id,
                Name = ingredient.Name
            });

        var roots = new List<IngredientListItemDto>();

        foreach (var ingredient in ingredients)
        {
            var current = allItems[ingredient.Id];
            if (ingredient.ParentId.HasValue && allItems.TryGetValue(ingredient.ParentId.Value, out var parent))
            {
                parent.Children.Add(current);
            }
            else
            {
                roots.Add(current);
            }
        }

        return roots;
    }
}
