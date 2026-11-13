using System.Text.Json.Serialization;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Dtos.Recipes;

public class GeneratedRecipeIngredientDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<IngredientUnit>))]
    public IngredientUnit Unit { get; set; }

    public int Quantity { get; set; }
}