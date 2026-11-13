using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sizzle.Domain.Entities;

namespace Sizzle.WebApi.Requests.Recipes;

public class NewIngredientRequestModel
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public Guid? ParentId { get; init; }

    [BindRequired]
    [EnumDataType(typeof(IngredientUnit))]
    public IngredientUnit Unit { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
