using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure;

public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        await InitializeDefaultIngredientsAsync(context);
    }

    private static async Task InitializeDefaultIngredientsAsync(ApplicationDbContext context)
    {
        if (await context.Ingredients.AnyAsync())
        {
            return;
        }

        const string resourceName = "Sizzle.Infrastructure.InitialData.default-ingredients.json";
        var json = await GetResourceAsync(resourceName);
        var ingredients = JsonSerializer.Deserialize<List<Ingredient>>(json);

        if (ingredients == null || ingredients.Count == 0)
        {
            return;
        }

        await context.Ingredients.AddRangeAsync(ingredients);

        await context.SaveChangesAsync();
    }

    private static async Task<string> GetResourceAsync(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
