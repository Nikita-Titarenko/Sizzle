using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure;

public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        await InitializeDefaultRolesAsync(context);
        await InitializeDefaultIngredientsAsync(context);
    }

    private static async Task InitializeDefaultRolesAsync(ApplicationDbContext context)
    {
        if (!await context.Roles.AnyAsync(r => r.Name == DefaultRoles.UserRole.Name))
        {
            await context.Roles.AddAsync(DefaultRoles.UserRole);
        }

        if (!await context.Roles.AnyAsync(r => r.Name == DefaultRoles.AdminRole.Name))
        {
            await context.Roles.AddAsync(DefaultRoles.AdminRole);
        }

        await context.SaveChangesAsync();
    }

    private static async Task InitializeDefaultIngredientsAsync(ApplicationDbContext context)
    {
        if (await context.Ingredients.AnyAsync())
        {
            return;
        }

        const string resourceName = "Sizzle.Infrastructure.InitialData.default-ingredients.json";
        var json = await GetResourceAsync(resourceName);
        var ingredients = JsonSerializer.Deserialize<List<DefaultIngredientSeed>>(json);

        if (ingredients == null || ingredients.Count == 0)
        {
            return;
        }

        foreach (var ingredient in ingredients)
        {
            await context.Ingredients.AddAsync(new Ingredient
            {
                Id = ingredient.Id,
                Name = ingredient.Name,
                ParentId = ingredient.ParentId
            });
        }

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

    private sealed class DefaultIngredientSeed
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public Guid? ParentId { get; init; }
    }
}
