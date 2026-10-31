using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Options;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services;
using Sizzle.Application.Services.Comments;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class RecipeServiceTests
{
    private readonly IRecipeRepository recipes = Substitute.For<IRecipeRepository>();
    private readonly IIngredientRepository ingredients = Substitute.For<IIngredientRepository>();
    private readonly ICategoryRepository categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileService files = Substitute.For<IFileService>();
    private readonly IRecipeNotificationSender notifications = Substitute.For<IRecipeNotificationSender>();
    private readonly IIngredientUnitFormatter formatter = Substitute.For<IIngredientUnitFormatter>();
    private readonly IRecipeReadCache cache = Substitute.For<IRecipeReadCache>();
    private readonly IRecipeInteractionBuffer interactions = Substitute.For<IRecipeInteractionBuffer>();
    private readonly IIngredientReadCache ingredientCache = Substitute.For<IIngredientReadCache>();
    private readonly ICommentReadCache commentCache = Substitute.For<ICommentReadCache>();
    private readonly IMapper mapper = Substitute.For<IMapper>();

    private RecipeService CreateService(int cooldownMinutes = 10) => new(
        recipes, ingredients, categories, unitOfWork, files, notifications,
        Microsoft.Extensions.Options.Options.Create(new BusinessLogicOptions { RecipeViewUpdateIntervalMinutes = cooldownMinutes }), formatter,
        NullLogger<PersonalizedRecipesLog>.Instance, Microsoft.Extensions.Options.Options.Create(new RecipeLoggingOptions()), cache,
        interactions, ingredientCache, commentCache, mapper, NullLogger<RecipeService>.Instance);

    [Fact]
    public async Task CreateRecipeAsync_InvalidCookingTimeDoesNotAccessRepositoriesOrCommit()
    {
        var result = await CreateService().CreateRecipeAsync(new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), Name = "Soup", Description = "Broth", CookingTimeMinutes = 0,
            CategoryId = Guid.NewGuid()
        });

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await categories.DidNotReceive().ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRecipeAsync_MissingCategoryReturnsNotFoundWithoutWriting()
    {
        using var source = new CancellationTokenSource();
        var input = NewRecipeInput();

        var result = await CreateService().CreateRecipeAsync(input, source.Token);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await categories.Received(1).ExistsAsync(input.CategoryId, source.Token);
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
    }

    [Fact]
    public async Task CreateRecipeAsync_MissingSelectedIngredientReturnsNotFoundWithoutWriting()
    {
        var ingredientId = Guid.NewGuid();
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), Name = "Soup", Description = "Broth", CookingTimeMinutes = 10,
            CategoryId = Guid.NewGuid(), Ingredients = [new RecipeIngredientInputDto { Id = ingredientId, Quantity = 2 }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetByIdsWithTranslationsAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteRecipeAsync_RejectsOtherOwnerWithoutRemovingOrInvalidating()
    {
        var id = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var user = Guid.NewGuid();
        recipes.GetWithDetailsAsync(id, null, Arg.Any<CancellationToken>())
            .Returns(new Recipe { Id = id, ApplicationUserId = owner });

        var result = await CreateService().DeleteRecipeAsync(id, user);

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        recipes.DidNotReceive().Remove(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await commentCache.DidNotReceive().InvalidateAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task DeleteRecipeAsync_RemovesOwnedRecipeCommitsAndInvalidatesCaches()
    {
        using var source = new CancellationTokenSource();
        var owner = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), ApplicationUserId = owner, FilePath = "recipe.jpg" };
        recipe.RecipeSteps.Add(new RecipeStep { Files = [new RecipeStepFile { FilePath = "step.jpg" }] });
        recipes.GetWithDetailsAsync(recipe.Id, null, source.Token).Returns(recipe);

        var result = await CreateService().DeleteRecipeAsync(recipe.Id, owner, source.Token);

        Assert.True(result.IsSuccess);
        recipes.Received(1).Remove(recipe);
        await unitOfWork.Received(1).CommitAsync(source.Token);
        files.Received(1).DeleteFile("recipe.jpg");
        files.Received(1).DeleteFile("step.jpg");
        await cache.Received(1).UpdateListsAsync(Arg.Is<RecipeListMutation>(mutation => mutation.Before != null
            && mutation.After == null), null, source.Token);
        await commentCache.Received(1).InvalidateAsync(recipe.Id);
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenBufferDisabled_UsesRepositoryAndReturnsState()
    {
        using var source = new CancellationTokenSource();
        var input = new ToggleLikeRecipeDto { RecipeId = Guid.NewGuid(), ApplicationUserId = Guid.NewGuid() };
        recipes.ToggleLikeAsync(input.ApplicationUserId, input.RecipeId, source.Token).Returns(true);

        var result = await CreateService().ToggleLikeAsync(input, source.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(input.RecipeId, result.Value!.RecipeId);
        Assert.True(result.Value.IsLiked);
        await recipes.Received(1).ToggleLikeAsync(input.ApplicationUserId, input.RecipeId, source.Token);
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenBufferEnabledAndRecipeMissing_DoesNotCallRepository()
    {
        interactions.Enabled.Returns(true);
        var input = new ToggleLikeRecipeDto { RecipeId = Guid.NewGuid(), ApplicationUserId = Guid.NewGuid() };

        var result = await CreateService().ToggleLikeAsync(input);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await interactions.Received(1).ToggleLikeAsync(input.ApplicationUserId, input.RecipeId, Arg.Any<CancellationToken>());
        await recipes.DidNotReceive().ToggleLikeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleSaveAsync_UpdatesExistingLinkAndPreservesSavedTimestampRules()
    {
        var recipeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var link = new ApplicationUserRecipe { RecipeId = recipeId, ApplicationUserId = userId };
        recipes.GetAsync(Arg.Any<CancellationToken>(), recipeId).Returns(new Recipe { Id = recipeId });
        recipes.GetUserRecipeAsync(userId, recipeId, Arg.Any<CancellationToken>()).Returns(link);
        var service = CreateService();
        var before = DateTime.UtcNow;

        var saved = await service.ToggleSaveAsync(recipeId, userId);
        var savedAt = saved.Value!.IsSavedAt;
        var unsaved = await service.ToggleSaveAsync(recipeId, userId);

        Assert.True(saved.Value.IsSaved);
        Assert.InRange(savedAt!.Value, before, DateTime.UtcNow);
        Assert.False(unsaved.Value!.IsSaved);
        Assert.Null(unsaved.Value.IsSavedAt);
        Assert.Null(link.IsSavedAt);
        recipes.DidNotReceive().AddUserRecipe(Arg.Any<ApplicationUserRecipe>());
        await unitOfWork.Received(2).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSavedRecipesAsync_AppliesPendingUnsavesAndSortsPendingSaves()
    {
        using var source = new CancellationTokenSource();
        var user = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var query = new GetRecipesDto { ApplicationUserId = user, PageSize = 10 };
        interactions.Enabled.Returns(true);
        interactions.GetPendingSavedAsync(user, source.Token).Returns(
        [
            (oldId, false, (DateTime?)null),
            (newId, true, (DateTime?)DateTime.UnixEpoch.AddDays(2))
        ]);
        recipes.GetSavedRecipeIdsAsync(query, Arg.Any<Guid[]>(), source.Token).Returns(
        [
            (oldId, (DateTime?)DateTime.UnixEpoch),
            (newId, (DateTime?)null)
        ]);
        recipes.GetByIdsWithAuthorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), user, source.Token)
            .Returns([new Recipe { Id = newId }]);

        var result = await CreateService().GetSavedRecipesAsync(query, source.Token);

        Assert.Equal(1, result.TotalSize);
        await recipes.Received(1).GetByIdsWithAuthorAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids =>
            ids.Count == 1 && ids.Contains(newId)), user, source.Token);
        await cache.DidNotReceive().GetOrCreateAsync(Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, Task<PageResponse<RecipeDto>>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateViewAsync_SkipsCommitWithinCooldown()
    {
        var recipeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var recipe = new Recipe { Id = recipeId, ViewCount = 5 };
        var link = new ApplicationUserRecipe { RecipeId = recipeId, ApplicationUserId = userId,
            ViewUpdatedAt = DateTime.UtcNow };
        recipes.GetAsync(Arg.Any<CancellationToken>(), recipeId).Returns(recipe);
        recipes.GetUserRecipeAsync(userId, recipeId, Arg.Any<CancellationToken>()).Returns(link);

        await CreateService().UpdateViewAsync(new UpdateRecipeViewDto { RecipeId = recipeId, ApplicationUserId = userId });

        Assert.Equal(5, recipe.ViewCount);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRecipesAsync_ClonesCachedPageBeforeApplyingAggregateInteractions()
    {
        using var source = new CancellationTokenSource();
        var recipe = new RecipeDto { Id = Guid.NewGuid(), LikeCount = 3 };
        var cached = new PageResponse<RecipeDto> { Data = [recipe], TotalSize = 1, PageSize = 10 };
        cache.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<PageResponse<RecipeDto>>>>(), source.Token)
            .Returns(cached);
        interactions.ApplyAsync(Arg.Any<IReadOnlyCollection<RecipeDto>>(), null, source.Token)
            .Returns(call =>
            {
                call.ArgAt<IReadOnlyCollection<RecipeDto>>(0).Single().LikeCount++;
                return Task.CompletedTask;
            });
        var result = await CreateService().GetRecipesAsync(new GetRecipesDto { PageSize = 10 }, source.Token);

        Assert.Equal(4, Assert.Single(result.Data).LikeCount);
        Assert.Equal(3, recipe.LikeCount);
        Assert.NotSame(recipe, result.Data[0]);
        await interactions.Received(1).ApplyAsync(Arg.Any<IReadOnlyCollection<RecipeDto>>(), null, source.Token);
    }

    [Fact]
    public async Task GetRecipesAsync_SharesCacheKeyAcrossViewersAndExcludesPrivateFields()
    {
        var keys = new List<string>();
        cache.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<PageResponse<RecipeDto>>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                keys.Add(call.ArgAt<string>(0));
                return Task.FromResult(new PageResponse<RecipeDto>
                {
                    Data = [new RecipeDto { Id = Guid.NewGuid() }], PageSize = 10
                });
            });

        var service = CreateService();
        var first = await service.GetRecipesAsync(new GetRecipesDto { ApplicationUserId = Guid.NewGuid(), PageSize = 10 });
        var second = await service.GetRecipesAsync(new GetRecipesDto { ApplicationUserId = Guid.NewGuid(), PageSize = 10 });

        Assert.Equal(keys[0], keys[1]);
        Assert.DoesNotContain("ApplicationUserId\":\"", keys[0]);
        var json = JsonSerializer.Serialize(first.Data[0]);
        Assert.DoesNotContain("IsLiked", json);
        Assert.DoesNotContain("IsSaved", json);
        Assert.DoesNotContain("IsFollowed", json);
        Assert.Single(second.Data);
    }

    private static CreateRecipeDto NewRecipeInput() => new()
    {
        ApplicationUserId = Guid.NewGuid(), Name = "Soup", Description = "Broth", CookingTimeMinutes = 10,
        CategoryId = Guid.NewGuid()
    };
}
