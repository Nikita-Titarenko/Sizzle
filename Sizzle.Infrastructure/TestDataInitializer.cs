using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Extensions;

namespace Sizzle.Infrastructure;

public static class TestDataInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IFileService fileService)
    {
        var users = await ReadAsync<List<UserSeed>>("app-users.json");
        var recipes = await ReadAsync<List<RecipeSeed>>("recipes.json");
        var ingredientCatalog = await ReadCatalogIdsAsync("ingredients.json");
        var categoryCatalog = await ReadCatalogIdsAsync("categories.json");

        if (users.Count != 20 || users.Any(user => user.Id == Guid.Empty || string.IsNullOrWhiteSpace(user.Name)
                || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
            || users.Select(user => user.Id).Distinct().Count() != users.Count
            || users.Select(user => user.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count() != users.Count)
        {
            throw new InvalidOperationException("Invalid test users in app-users.json.");
        }

        var userIds = users.Select(user => user.Id).ToHashSet();
        if (recipes.Count == 0 || recipes.Select(recipe => recipe.Id).Distinct().Count() != recipes.Count
            || recipes.Any(recipe => recipe.Id == Guid.Empty || !userIds.Contains(recipe.ApplicationUserId)
                || !categoryCatalog.Contains(recipe.CategoryId) || string.IsNullOrWhiteSpace(recipe.Name)
                || string.IsNullOrWhiteSpace(recipe.Description) || recipe.CookingTimeMinutes <= 0
                || recipe.Ingredients is not { Length: > 0 } || recipe.Steps is not { Length: > 0 }
                || recipe.Ingredients.Any(ingredient => !ingredientCatalog.Contains(ingredient.IngredientId) || ingredient.Quantity <= 0)
                || recipe.Ingredients.Select(ingredient => ingredient.IngredientId).Distinct().Count() != recipe.Ingredients.Length
                || recipe.Steps.Any(step => string.IsNullOrWhiteSpace(step.Name) || string.IsNullOrWhiteSpace(step.Description))))
        {
            throw new InvalidOperationException("Invalid test recipes in recipes.json.");
        }

        var media = ReadMedia(recipes, fileService);

        var existingUserIds = await context.Users.Select(user => user.Id).ToHashSetAsync();
        var existingEmails = await context.Users.Select(user => new { user.Id, user.Email }).ToListAsync();
        foreach (var user in users)
        {
            if (existingUserIds.Contains(user.Id))
            {
                if (existingEmails.Any(existing => existing.Id == user.Id
                    && !string.Equals(existing.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Test user ID '{user.Id}' is assigned to another email.");
                }

                continue;
            }

            if (existingEmails.Any(existing => string.Equals(existing.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Test email '{user.Email}' belongs to another user.");
            }

            var result = await userManager.CreateAsync(new ApplicationUser
            {
                Id = user.Id,
                Name = user.Name,
                UserName = user.Email,
                Email = user.Email,
                EmailConfirmed = true
            }, user.Password);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed test user '{user.Email}': {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }

        var existingRecipes = await context.Recipes
            .WhereIdIn(recipes.Select(recipe => recipe.Id), recipe => recipe.Id)
            .Include(recipe => recipe.RecipeSteps).ThenInclude(step => step.Files)
            .AsSplitQuery()
            .ToDictionaryAsync(recipe => recipe.Id);
        var newRecipes = recipes.Where(recipe => !existingRecipes.ContainsKey(recipe.Id))
            .Select(recipe => new Recipe
            {
                Id = recipe.Id,
                ApplicationUserId = recipe.ApplicationUserId,
                CategoryId = recipe.CategoryId,
                Name = recipe.Name,
                Description = recipe.Description,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                CreationDate = DateTime.UtcNow,
                RecipeIngredients = recipe.Ingredients.Select(ingredient => new RecipeIngredient
                {
                    IngredientId = ingredient.IngredientId,
                    Quantity = ingredient.Quantity
                }).ToList(),
                RecipeSteps = recipe.Steps.Select((step, position) => new RecipeStep
                {
                    Id = Guid.NewGuid(),
                    Name = step.Name,
                    Description = step.Description,
                    Position = position
                }).ToList()
            }).ToList();
        await context.Recipes.AddRangeAsync(newRecipes);

        var seededRecipes = existingRecipes.Values.Concat(newRecipes).ToDictionary(recipe => recipe.Id);
        foreach (var asset in media)
        {
            var recipe = seededRecipes[asset.RecipeId];
            var path = Path.Combine("recipes", recipe.Id.ToString("N"), asset.FileName);

            if (asset.StepNumber is null)
            {
                if (recipe.FilePath != null && recipe.FilePath != path)
                {
                    continue;
                }

                await SaveMediaIfMissingAsync(asset.ResourceName, path, fileService);
                recipe.FilePath = path;
                continue;
            }

            var step = recipe.RecipeSteps.FirstOrDefault(item => item.Position == asset.StepNumber.Value - 1);
            if (step == null)
            {
                continue;
            }

            await SaveMediaIfMissingAsync(asset.ResourceName, path, fileService);
            if (step.Files.All(file => file.FilePath != path))
            {
                step.Files.Add(new RecipeStepFile { Id = Guid.NewGuid(), FilePath = path });
            }
        }

        await context.SaveChangesAsync();
    }

    private static List<MediaSeed> ReadMedia(IReadOnlyCollection<RecipeSeed> recipes, IFileService fileService)
    {
        var recipesById = recipes.ToDictionary(recipe => recipe.Id);
        var assembly = typeof(TestDataInitializer).Assembly;
        var result = new List<MediaSeed>();
        var declaredPaths = recipes.SelectMany(recipe =>
            (recipe.FilePath == null ? Array.Empty<string>() : new[] { recipe.FilePath })
                .Concat(recipe.Steps.SelectMany(step => step.FilePaths))).ToArray();
        if (declaredPaths.Any(string.IsNullOrWhiteSpace)
            || declaredPaths.Distinct(StringComparer.Ordinal).Count() != declaredPaths.Length)
        {
            throw new InvalidOperationException("Invalid or duplicate media paths in recipes.json.");
        }

        var expectedPaths = declaredPaths.ToHashSet(StringComparer.Ordinal);

        foreach (var folder in new[] { "recipes-media", "recipe-steps-media" })
        {
            var prefix = $"Sizzle.Infrastructure.TestData.{folder.Replace('-', '_')}.";
            foreach (var resourceName in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
            {
                var fileName = resourceName[prefix.Length..];
                var stepNumber = (int?)null;
                var imageNumber = 0;
                Guid recipeId;

                if (folder == "recipe-steps-media")
                {
                    var match = Regex.Match(fileName, @"^step-(\d+)-(\d+)-([0-9a-fA-F-]{36})\.[^.]+$");
                    if (!match.Success || !int.TryParse(match.Groups[1].Value, out var step) || step <= 0
                        || !int.TryParse(match.Groups[2].Value, out imageNumber) || imageNumber <= 0
                        || !Guid.TryParse(match.Groups[3].Value, out recipeId))
                    {
                        throw new InvalidOperationException($"Invalid step media name '{fileName}'.");
                    }

                    stepNumber = step;
                }
                else if (!Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out recipeId))
                {
                    throw new InvalidOperationException($"Invalid recipe media name '{fileName}'.");
                }

                if (!recipesById.TryGetValue(recipeId, out var recipe)
                    || stepNumber > recipe.Steps.Length)
                {
                    throw new InvalidOperationException($"Media '{fileName}' references an unknown recipe or step.");
                }

                var path = $"recipes/{recipeId:N}/{fileName}";
                if (!expectedPaths.Contains(path)
                    || (stepNumber is null ? recipe.FilePath != path
                        : !recipe.Steps[stepNumber.Value - 1].FilePaths.Contains(path)))
                {
                    throw new InvalidOperationException($"Media '{fileName}' is not assigned to its recipe or step in recipes.json.");
                }

                using var stream = assembly.GetManifestResourceStream(resourceName)!;
                if (!fileService.IsValidMedia(fileName, stream.Length))
                {
                    throw new InvalidOperationException($"Unsupported test media '{fileName}'.");
                }

                result.Add(new MediaSeed(recipeId, stepNumber, imageNumber, resourceName, fileName));
            }
        }

        if (result.Where(item => item.StepNumber is null).GroupBy(item => item.RecipeId).Any(group => group.Count() > 1)
            || result.Where(item => item.StepNumber is not null)
                .GroupBy(item => (item.RecipeId, item.StepNumber, item.ImageNumber)).Any(group => group.Count() > 1)
            || !expectedPaths.SetEquals(result.Select(item => $"recipes/{item.RecipeId:N}/{item.FileName}")))
        {
            throw new InvalidOperationException("Duplicate or missing recipe or step media in TestData.");
        }

        return result.OrderBy(item => item.RecipeId).ThenBy(item => item.StepNumber)
            .ThenBy(item => item.ImageNumber).ToList();
    }

    private static async Task SaveMediaIfMissingAsync(string resourceName, string path, IFileService fileService)
    {
        if (fileService.GetFileUrl(path) != null)
        {
            return;
        }

        await using var stream = typeof(TestDataInitializer).Assembly.GetManifestResourceStream(resourceName)!;
        await fileService.SaveFile(stream, path);
    }

    private sealed record MediaSeed(Guid RecipeId, int? StepNumber, int ImageNumber, string ResourceName, string FileName);

    private static async Task<HashSet<Guid>> ReadCatalogIdsAsync(string resourceName)
    {
        var catalog = await ReadAsync<List<CatalogSeed>>(resourceName, "InitialData");
        return catalog.Select(item => item.Id).ToHashSet();
    }

    private static async Task<T> ReadAsync<T>(string resourceName, string folder = "TestData")
    {
        var fullName = $"Sizzle.Infrastructure.{folder}.{resourceName}";
        await using var stream = typeof(TestDataInitializer).Assembly.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Resource '{fullName}' not found.");
        return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Resource '{fullName}' is empty.");
    }

    private sealed class CatalogSeed
    {
        public Guid Id { get; set; }
    }

    private sealed class UserSeed
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private sealed class RecipeSeed
    {
        public Guid Id { get; set; }
        public Guid ApplicationUserId { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CookingTimeMinutes { get; set; }
        public string? FilePath { get; set; }
        public IngredientSeed[] Ingredients { get; set; } = [];
        public StepSeed[] Steps { get; set; } = [];
    }

    private sealed class IngredientSeed
    {
        public Guid IngredientId { get; set; }
        public int Quantity { get; set; }
    }

    private sealed class StepSeed
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] FilePaths { get; set; } = [];
    }
}
