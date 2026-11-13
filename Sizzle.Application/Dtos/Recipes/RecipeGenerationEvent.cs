using Sizzle.Domain.Common;

namespace Sizzle.Application.Dtos.Recipes;

public sealed record RecipeGenerationEvent(string Type, string? Text = null, GenerateRecipeResponseDto? Result = null, IReadOnlyList<Error>? Errors = null);