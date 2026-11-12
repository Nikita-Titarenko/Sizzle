using Sizzle.Application.Dtos.Recipes;
using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Repositories;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class RecipeRepositoryTests : InMemoryRepositoryTest
{
    [Fact]
    public async Task GetRecipesAsync_FiltersByOwnerCategoryIngredientAndStepText_ThenOrdersAndPages()
    {
        var owner = new ApplicationUser { Id = Guid.NewGuid(), Name = "Chef" };
        var other = new ApplicationUser { Id = Guid.NewGuid(), Name = "Other" };
        var categoryId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var first = NewRecipe(owner.Id, categoryId, "Soup", 8, ingredientId);
        var second = NewRecipe(owner.Id, categoryId, "Stew", 5, ingredientId);
        second.RecipeSteps.Add(new RecipeStep { Id = Guid.NewGuid(), Name = "Simmer", Description = "herbs" });
        first.RecipeSteps.Add(new RecipeStep { Id = Guid.NewGuid(), Name = "Mix", Description = "herbs" });
        var unrelated = NewRecipe(other.Id, categoryId, "Herbs", 20, ingredientId);
        var wrongIngredient = NewRecipe(owner.Id, categoryId, "Herbs", 50, Guid.NewGuid());
        await using (var seed = CreateContext())
        {
            seed.Users.AddRange(owner, other);
            seed.Recipes.AddRange(second, unrelated, wrongIngredient, first);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var page = await new RecipeRepository(db).GetRecipesAsync(new GetRecipesDto
        {
            OwnerId = owner.Id,
            CategoryIds = [categoryId],
            IngredientIds = [ingredientId],
            Search = " herbs ",
            PageIndex = 1,
            PageSize = 1
        });

        Assert.Equal(2, page.TotalSize);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(second.Id, Assert.Single(page.Data).Id);
        Assert.Equal("Chef", page.Data[0].ApplicationUser!.Name);
    }

    [Fact]
    public async Task GetRecipesAsync_DoesNotLoadViewerInteractionsOrFollowState()
    {
        var viewer = new ApplicationUser { Id = Guid.NewGuid() };
        var otherViewer = new ApplicationUser { Id = Guid.NewGuid() };
        var author = new ApplicationUser { Id = Guid.NewGuid(), Name = "Author" };
        var recipe = NewRecipe(author.Id, Guid.NewGuid(), "Cake", 1);
        await using (var seed = CreateContext())
        {
            seed.Users.AddRange(viewer, otherViewer, author);
            seed.Recipes.Add(recipe);
            seed.ApplicationUserRecipes.AddRange(
                new ApplicationUserRecipe { RecipeId = recipe.Id, ApplicationUserId = viewer.Id, IsLiked = true },
                new ApplicationUserRecipe { RecipeId = recipe.Id, ApplicationUserId = otherViewer.Id, IsSaved = true });
            seed.UserFollowers.AddRange(
                new UserFollower { FollowerId = viewer.Id, FollowingId = author.Id },
                new UserFollower { FollowerId = otherViewer.Id, FollowingId = author.Id });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var page = await new RecipeRepository(db).GetRecipesAsync(new GetRecipesDto
        {
            ApplicationUserId = viewer.Id,
            PageSize = 10
        });

        var loaded = Assert.Single(page.Data);
        Assert.Empty(loaded.ApplicationUserRecipes);
        Assert.Empty(loaded.ApplicationUser!.Followers);
    }

    [Fact]
    public async Task GetSavedRecipesAsync_OrdersBySaveTimeAndExcludesOtherUsersAndUnsavedLinks()
    {
        var viewer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var earlier = NewRecipe(other, categoryId, "Earlier", 0);
        var later = NewRecipe(other, categoryId, "Later", 0);
        var notSaved = NewRecipe(other, categoryId, "Not saved", 0);
        await using (var seed = CreateContext())
        {
            seed.Users.AddRange(new ApplicationUser { Id = viewer }, new ApplicationUser { Id = other });
            seed.Recipes.AddRange(earlier, later, notSaved);
            seed.ApplicationUserRecipes.AddRange(
                new ApplicationUserRecipe { ApplicationUserId = viewer, RecipeId = earlier.Id, IsSaved = true, IsSavedAt = DateTime.UnixEpoch },
                new ApplicationUserRecipe { ApplicationUserId = viewer, RecipeId = later.Id, IsSaved = true, IsSavedAt = DateTime.UnixEpoch.AddDays(1) },
                new ApplicationUserRecipe { ApplicationUserId = viewer, RecipeId = notSaved.Id },
                new ApplicationUserRecipe { ApplicationUserId = other, RecipeId = notSaved.Id, IsSaved = true, IsSavedAt = DateTime.UnixEpoch.AddDays(2) });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new RecipeRepository(db);
        var query = new GetRecipesDto { ApplicationUserId = viewer, PageSize = 1 };
        var firstPage = await repository.GetSavedRecipesAsync(query);
        var ids = await repository.GetSavedRecipeIdsAsync(query, [notSaved.Id]);

        Assert.Equal(2, firstPage.TotalSize);
        Assert.Equal(later.Id, Assert.Single(firstPage.Data).Id);
        Assert.Equal(3, ids.Count);
        Assert.Null(Assert.Single(ids, entry => entry.Id == notSaved.Id).SavedAt);
        Assert.Equal(DateTime.UnixEpoch, Assert.Single(ids, entry => entry.Id == earlier.Id).SavedAt);
    }

    [Fact]
    public async Task GetFollowingRecipesAsync_ReturnsNewestFirstAndBuildsCacheEntries()
    {
        var viewer = new ApplicationUser { Id = Guid.NewGuid(), Name = "Viewer" };
        var followedA = new ApplicationUser { Id = Guid.NewGuid(), Name = "A" };
        var followedB = new ApplicationUser { Id = Guid.NewGuid(), Name = "B" };
        var other = new ApplicationUser { Id = Guid.NewGuid(), Name = "Other" };
        var first = NewRecipe(followedA.Id, Guid.NewGuid(), "First", 0);
        first.CreationDate = DateTime.UnixEpoch.AddHours(1);
        var latest = NewRecipe(followedB.Id, Guid.NewGuid(), "Latest", 0);
        latest.CreationDate = DateTime.UnixEpoch.AddHours(2);
        var unrelated = NewRecipe(other.Id, Guid.NewGuid(), "Other", 0);
        unrelated.CreationDate = DateTime.UnixEpoch.AddHours(3);
        await using (var seed = CreateContext())
        {
            seed.Users.AddRange(viewer, followedA, followedB, other);
            seed.Recipes.AddRange(first, latest, unrelated);
            seed.UserFollowers.AddRange(
                new UserFollower { FollowerId = viewer.Id, FollowingId = followedA.Id },
                new UserFollower { FollowerId = viewer.Id, FollowingId = followedB.Id });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new RecipeRepository(db);
        var page = await repository.GetFollowingRecipesAsync(new GetRecipesDto
        {
            ApplicationUserId = viewer.Id,
            PageIndex = 0,
            PageSize = 10
        });
        var entries = await repository.GetFollowingFeedEntriesAsync(viewer.Id, 10);

        Assert.Equal(2, page.TotalSize);
        Assert.Equal([latest.Id, first.Id], page.Data.Select(recipe => recipe.Id));
        Assert.Equal([latest.Id, first.Id], entries.Select(item => item.RecipeId));
    }

    [Fact]
    public async Task GetRecommendationCandidatesAsync_ExcludesCurrentAuthorAndAppliesIngredientCategoryFilters()
    {
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var category = Guid.NewGuid();
        var ingredient = Guid.NewGuid();
        var included = NewRecipe(other, category, "Included", 0, ingredient);
        var own = NewRecipe(user, category, "Own", 0, ingredient);
        var otherCategory = NewRecipe(other, Guid.NewGuid(), "Other category", 0, ingredient);
        await using (var seed = CreateContext())
        {
            seed.Recipes.AddRange(included, own, otherCategory);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var candidates = await new RecipeRepository(db).GetRecommendationCandidatesAsync(new GetRecipesDto
        {
            ApplicationUserId = user,
            CategoryIds = [category],
            IngredientIds = [ingredient]
        });

        var candidate = Assert.Single(candidates);
        Assert.Equal(included.Id, candidate.Id);
        Assert.Equal([ingredient], candidate.IngredientIds);
    }

    [Fact]
    public async Task GetWithFullDetailsAsync_LoadsStepsIngredientTranslationsAndViewerState()
    {
        var author = new ApplicationUser { Id = Guid.NewGuid() };
        var viewer = new ApplicationUser { Id = Guid.NewGuid() };
        var recipe = NewRecipe(author.Id, Guid.NewGuid(), "Soup", 0);
        recipe.RecipeSteps.Add(new RecipeStep
        {
            Id = Guid.NewGuid(),
            Name = "Cook",
            Files = [new RecipeStepFile { Id = Guid.NewGuid(), FilePath = "video.mp4" }]
        });
        var ingredientId = Guid.NewGuid();
        recipe.RecipeIngredients.Add(new RecipeIngredient
        {
            IngredientId = ingredientId,
            Ingredient = new Ingredient { Id = ingredientId, Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Salt" }] }
        });
        await using (var seed = CreateContext())
        {
            seed.Users.AddRange(author, viewer);
            seed.Recipes.Add(recipe);
            seed.ApplicationUserRecipes.Add(new ApplicationUserRecipe { ApplicationUserId = viewer.Id, RecipeId = recipe.Id, IsSaved = true });
            seed.UserFollowers.Add(new UserFollower { FollowerId = viewer.Id, FollowingId = author.Id });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var loaded = await new RecipeRepository(db).GetWithFullDetailsAsync(recipe.Id, viewer.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Cook", Assert.Single(loaded.RecipeSteps).Name);
        Assert.Equal("video.mp4", Assert.Single(loaded.RecipeSteps.Single().Files).FilePath);
        Assert.Equal("Salt", Assert.Single(loaded.RecipeIngredients.Single().Ingredient.Translations).Name);
        Assert.True(Assert.Single(loaded.ApplicationUserRecipes).IsSaved);
        Assert.Equal(viewer.Id, Assert.Single(loaded.ApplicationUser!.Followers).FollowerId);
    }

    [Fact]
    public async Task ToggleLikeAsync_PersistsLikeAndUnlikeWithoutDuplicateLink()
    {
        var recipe = NewRecipe(Guid.NewGuid(), Guid.NewGuid(), "Soup", 0);
        var viewerId = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            seed.Recipes.Add(recipe);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repository = new RecipeRepository(db);

        Assert.Null(await repository.ToggleLikeAsync(viewerId, Guid.NewGuid()));
        Assert.True(await repository.ToggleLikeAsync(viewerId, recipe.Id));
        Assert.False(await repository.ToggleLikeAsync(viewerId, recipe.Id));

        await using var verify = CreateContext();
        Assert.Equal(0, (await verify.Recipes.FindAsync(recipe.Id))!.LikeCount);
        var link = Assert.Single(verify.ApplicationUserRecipes);
        Assert.False(link.IsLiked);
        Assert.Equal(viewerId, link.ApplicationUserId);
    }

    private static Recipe NewRecipe(Guid authorId, Guid categoryId, string name, int likes, Guid? ingredientId = null)
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = authorId,
            CategoryId = categoryId,
            Name = name,
            LikeCount = likes,
            CreationDate = DateTime.UnixEpoch
        };
        if (ingredientId.HasValue)
            recipe.RecipeIngredients.Add(new RecipeIngredient { IngredientId = ingredientId.Value });
        return recipe;
    }
}
