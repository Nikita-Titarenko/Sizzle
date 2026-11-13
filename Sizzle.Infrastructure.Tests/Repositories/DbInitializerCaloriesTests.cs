using Microsoft.EntityFrameworkCore;
using Sizzle.Infrastructure;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class DbInitializerCaloriesTests
{
    [Fact]
    public async Task InitializeAsync_LoadsAndUpdatesSeedIngredientCalories()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);

        await DbInitializer.InitializeAsync(context);

        var ingredient = await context.Ingredients.SingleAsync(item => item.Id == Guid.Parse("5f9b74b2-6361-4e7a-8a69-5989a3ccf301"));
        Assert.Equal(400, ingredient.Calories);

        ingredient.Calories = 0;
        await context.SaveChangesAsync();
        await DbInitializer.InitializeAsync(context);

        Assert.Equal(400, ingredient.Calories);
    }
}