using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Ingredients;

public interface IIngredientUnitFormatter
{
    string Format(IngredientUnit unit, string languageCode);
}
