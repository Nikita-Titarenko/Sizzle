using System.Text.Json;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Services;

public sealed class IngredientUnitFormatter : IIngredientUnitFormatter
{
    private readonly Dictionary<string, Dictionary<string, string>> _units;

    public IngredientUnitFormatter()
    {
        const string resourceName = "Sizzle.Infrastructure.InitialData.ingredient-unit.json";
        using var stream = typeof(IngredientUnitFormatter).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");

        var translations = JsonSerializer.Deserialize<List<Dictionary<string, Dictionary<string, string>>>>(stream)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' is empty.");

        _units = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var translation in translations)
        {
            foreach (var (language, values) in translation)
            {
                if (!_units.TryAdd(language, values))
                {
                    throw new InvalidOperationException($"Duplicate language '{language}' in resource '{resourceName}'.");
                }
            }
        }

        foreach (var (language, values) in _units)
        {
            if (Enum.GetNames<IngredientUnit>().Any(unit => !values.TryGetValue(unit.ToLowerInvariant(), out var value)
                || string.IsNullOrWhiteSpace(value)))
            {
                throw new InvalidOperationException($"Missing ingredient unit translation for '{language}' in resource '{resourceName}'.");
            }
        }

        if (!_units.ContainsKey("en"))
        {
            throw new InvalidOperationException($"English translations are missing in resource '{resourceName}'.");
        }
    }

    public string Format(IngredientUnit unit, string languageCode)
    {
        var values = _units.GetValueOrDefault(languageCode) ?? _units["en"];
        var key = unit.ToString().ToLowerInvariant();
        return values.TryGetValue(key, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown ingredient unit.");
    }
}
