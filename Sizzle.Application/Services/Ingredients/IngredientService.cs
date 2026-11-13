using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Ingredients;

public class IngredientService(IIngredientRepository ingredientRepository, IIngredientUnitFormatter unitFormatter,
    IIngredientReadCache cache) : IIngredientService
{
    private const int MinimumQuantity = 1;

    public async Task<IEnumerable<IngredientListItemDto>> GetAllAsync(GetAllIngredientsDto dto, CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync(dto, () => GetAllCoreAsync(dto, cancellationToken));

    public Result<IEnumerable<IngredientProportionItemDto>> CalculateProportions(CalculateIngredientProportionsDto dto)
    {
        var initial = dto.Initial?.ToList();
        if (initial is not { Count: > 0 } || initial.Any(item => item == null || item.Id == Guid.Empty || item.Quantity <= 0) ||
            initial.Select(item => item.Id).Distinct().Count() != initial.Count ||
            dto.ChangedIngredient is not { Id: var changedId, Quantity: > 0 } ||
            changedId == Guid.Empty)
        {
            return InvalidProportions("Invalid ingredient quantities");
        }

        var changed = initial.FirstOrDefault(item => item.Id == changedId);
        if (changed == null)
        {
            return InvalidProportions("Changed ingredient must be in initial ingredients");
        }

        var ratio = (decimal)dto.ChangedIngredient.Quantity / changed.Quantity;
        var scaled = new List<IngredientProportionItemDto>(initial.Count);
        foreach (var item in initial)
        {
            var quantity = item.Id == changedId
                ? dto.ChangedIngredient.Quantity
                : Math.Max(MinimumQuantity, decimal.Round(item.Quantity * ratio, MidpointRounding.AwayFromZero));
            if (quantity > int.MaxValue)
            {
                return InvalidProportions("Scaled ingredient quantity is too large");
            }

            scaled.Add(new IngredientProportionItemDto { Id = item.Id, Quantity = (int)quantity });
        }

        return new Result<IEnumerable<IngredientProportionItemDto>> { Value = scaled };
    }

    private static Result<IEnumerable<IngredientProportionItemDto>> InvalidProportions(string message) => new()
    {
        Errors = [new Error { Key = ErrorKey.BadRequest, Message = message }]
    };

    private async Task<List<IngredientListItemDto>> GetAllCoreAsync(GetAllIngredientsDto dto, CancellationToken cancellationToken)
    {
        var ingredients = (await ingredientRepository.GetAllOrderedAsync(dto.Search, dto.PageIndex, dto.PageSize, dto.LanguageCode, cancellationToken)).ToList();

        var allItems = ingredients.ToDictionary(
            ingredient => ingredient.Id,
            ingredient => new IngredientListItemDto
            {
                Id = ingredient.Id,
                Unit = unitFormatter.Format(ingredient.Unit, dto.LanguageCode),
                Calories = ingredient.Calories,
                Name = ingredient.Translations.FirstOrDefault(t => t.LanguageCode == dto.LanguageCode)?.Name
                    ?? ingredient.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                    ?? string.Empty
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
