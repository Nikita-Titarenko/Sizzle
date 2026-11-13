using AutoMapper;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Mapping;

public class RecipeDetailsIngredientsResolver(IIngredientUnitFormatter unitFormatter)
    : IValueResolver<Recipe, RecipeDetailsDto, IReadOnlyList<RecipeIngredientDto>>
{
    public IReadOnlyList<RecipeIngredientDto> Resolve(Recipe source, RecipeDetailsDto destination,
        IReadOnlyList<RecipeIngredientDto> member, ResolutionContext context)
    {
        var languageCode = (string)context.Items["LanguageCode"];
        return source.RecipeIngredients.Select(link => new RecipeIngredientDto
        {
            Id = link.IngredientId,
            Quantity = link.Quantity,
            Unit = unitFormatter.Format(link.Ingredient.Unit, languageCode),
            Name = link.Ingredient.Translations.FirstOrDefault(translation => translation.LanguageCode == languageCode)?.Name
                ?? link.Ingredient.Translations.FirstOrDefault(translation => translation.LanguageCode == "en")?.Name
                ?? string.Empty
        }).ToList();
    }
}