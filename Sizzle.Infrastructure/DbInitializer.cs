using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure;

public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        await InitializeDefaultIngredientsAsync(context);
        await InitializeDefaultCategoriesAsync(context);
    }

    private static async Task InitializeDefaultIngredientsAsync(ApplicationDbContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourcePrefix = "Sizzle.Infrastructure.InitialData.ingredients";
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .ToArray();

        var baseResource = $"{resourcePrefix}.json";
        if (!resources.Contains(baseResource))
        {
            throw new InvalidOperationException($"Resource '{baseResource}' not found.");
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        await using var baseStream = assembly.GetManifestResourceStream(baseResource)!;
        var ingredients = await JsonSerializer.DeserializeAsync<List<IngredientSeed>>(baseStream, options)
            ?? throw new InvalidOperationException($"Resource '{baseResource}' is empty.");
        var seedIds = ingredients.Select(i => i.Id).ToHashSet();
        if (seedIds.Count != ingredients.Count)
        {
            throw new InvalidOperationException($"Resource '{baseResource}' contains duplicate ingredient ids.");
        }

        if (ingredients.Any(i => i.Id == Guid.Empty || !Enum.IsDefined(i.Unit)))
        {
            throw new InvalidOperationException($"Resource '{baseResource}' contains invalid ingredient data.");
        }

        var existingIngredients = await context.Ingredients.ToListAsync();
        var existingById = existingIngredients.ToDictionary(i => i.Id);
        var newIngredients = ingredients
            .Where(i => !existingById.ContainsKey(i.Id))
            .Select(i => new Ingredient { Id = i.Id, ParentId = i.ParentId, Unit = i.Unit });
        await context.Ingredients.AddRangeAsync(newIngredients);

        foreach (var seed in ingredients)
        {
            if (existingById.TryGetValue(seed.Id, out var ingredient))
            {
                ingredient.Unit = seed.Unit;
            }
        }

        var existingTranslations = (await context.IngredientTranslations
            .Select(t => new { t.IngredientId, t.LanguageCode })
            .ToListAsync())
            .Select(t => (t.IngredientId, t.LanguageCode))
            .ToHashSet();

        foreach (var resource in resources.Where(name => name != baseResource))
        {
            var languageCode = resource[(resourcePrefix.Length + 1)..^".json".Length];
            if (string.IsNullOrWhiteSpace(languageCode) || languageCode.Contains('.'))
            {
                continue;
            }

            await using var stream = assembly.GetManifestResourceStream(resource)!;
            var translations = await JsonSerializer.DeserializeAsync<List<TranslationSeed>>(stream, options)
                ?? throw new InvalidOperationException($"Resource '{resource}' is empty.");

            foreach (var translation in translations)
            {
                if (!seedIds.Contains(translation.Id))
                {
                    throw new InvalidOperationException($"Unknown ingredient '{translation.Id}' in resource '{resource}'.");
                }

                if (string.IsNullOrWhiteSpace(translation.Name))
                {
                    throw new InvalidOperationException($"Missing ingredient name for '{translation.Id}' in resource '{resource}'.");
                }

                if (existingTranslations.Add((translation.Id, languageCode)))
                {
                    context.IngredientTranslations.Add(new IngredientTranslation
                    {
                        IngredientId = translation.Id,
                        LanguageCode = languageCode,
                        Name = translation.Name
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task InitializeDefaultCategoriesAsync(ApplicationDbContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourcePrefix = "Sizzle.Infrastructure.InitialData.categories";
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .ToArray();

        var baseResource = $"{resourcePrefix}.json";
        if (!resources.Contains(baseResource))
        {
            throw new InvalidOperationException($"Resource '{baseResource}' not found.");
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await using var baseStream = assembly.GetManifestResourceStream(baseResource)!;
        var categories = await JsonSerializer.DeserializeAsync<List<CategorySeed>>(baseStream, options)
            ?? throw new InvalidOperationException($"Resource '{baseResource}' is empty.");
        var seedIds = categories.Select(category => category.Id).ToHashSet();
        if (seedIds.Contains(Guid.Empty) || seedIds.Count != categories.Count)
        {
            throw new InvalidOperationException($"Resource '{baseResource}' contains invalid or duplicate category ids.");
        }

        var existingIds = await context.Categories.Select(category => category.Id).ToHashSetAsync();
        await context.Categories.AddRangeAsync(categories.Where(category => !existingIds.Contains(category.Id))
            .Select(category => new Category { Id = category.Id }));

        var existingTranslations = (await context.CategoryTranslations
            .Select(translation => new { translation.CategoryId, translation.LanguageCode })
            .ToListAsync())
            .Select(translation => (translation.CategoryId, translation.LanguageCode))
            .ToHashSet();

        foreach (var resource in resources.Where(name => name != baseResource))
        {
            var languageCode = resource[(resourcePrefix.Length + 1)..^".json".Length];
            if (string.IsNullOrWhiteSpace(languageCode) || languageCode.Contains('.'))
            {
                continue;
            }

            await using var stream = assembly.GetManifestResourceStream(resource)!;
            var translations = await JsonSerializer.DeserializeAsync<List<TranslationSeed>>(stream, options)
                ?? throw new InvalidOperationException($"Resource '{resource}' is empty.");
            if (translations.Count != seedIds.Count || translations.Select(t => t.Id).Distinct().Count() != seedIds.Count)
            {
                throw new InvalidOperationException($"Resource '{resource}' must contain each category once.");
            }

            foreach (var translation in translations)
            {
                if (!seedIds.Contains(translation.Id) || string.IsNullOrWhiteSpace(translation.Name))
                {
                    throw new InvalidOperationException($"Invalid category translation in resource '{resource}'.");
                }

                if (existingTranslations.Add((translation.Id, languageCode)))
                {
                    context.CategoryTranslations.Add(new CategoryTranslation
                    {
                        CategoryId = translation.Id,
                        LanguageCode = languageCode,
                        Name = translation.Name
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private sealed class CategorySeed
    {
        public Guid Id { get; set; }
    }

    private sealed class IngredientSeed
    {
        public Guid Id { get; set; }

        public Guid? ParentId { get; set; }

        public IngredientUnit Unit { get; set; }
    }

    private sealed class TranslationSeed
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
