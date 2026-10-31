using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Mapping;
using Sizzle.Application.Services;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Entities;
using Sizzle.WebApi.Mapping;
using Sizzle.WebApi.Requests.Recipes;
using Xunit;

namespace Sizzle.MappingTests;

public class MappingTests
{
    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileService, TestFileService>();
        services.AddSingleton<IIngredientUnitFormatter, TestUnitFormatter>();
        services.AddAutoMapper(_ => { }, typeof(RequestMappingProfile).Assembly, typeof(ApplicationMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    [Fact]
    public void RecipeFormMapsNestedInputsAndMedia()
    {
        var mapper = CreateMapper();
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "photo.jpg");
        var request = new CreateRecipeRequestModel
        {
            Name = "Recipe", Description = "Description", CookingTimeMinutes = 20,
            CategoryId = Guid.NewGuid(), File = file,
            Steps = [new RecipeStepRequestModel { Name = "Step", Description = "Do it", Files = [file] }],
            Ingredients = [new RecipeIngredientRequestModel { Id = Guid.NewGuid(), Quantity = 2 }],
            NewIngredients = []
        };
        var userId = Guid.NewGuid();

        var dto = mapper.Map<CreateRecipeDto>(request, options => options.Items["ApplicationUserId"] = userId);

        Assert.Equal(userId, dto.ApplicationUserId);
        Assert.Equal("photo.jpg", dto.File?.FileName);
        Assert.Equal(3, dto.File?.Length);
        Assert.Equal("photo.jpg", dto.Steps.Single().Files.Single().FileName);
        Assert.Equal("Step", dto.Steps.Single().Name);
        Assert.Equal(2, dto.Ingredients.Single().Quantity);
    }

    [Fact]
    public void RecipeQueryPreservesOwnerAndViewer()
    {
        var mapper = CreateMapper();
        var viewerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var request = new GetRecipesRequestModel { UserId = Guid.NewGuid(), PageSize = 9 };
        var dto = mapper.Map(request, new GetRecipesDto { ApplicationUserId = viewerId, OwnerId = ownerId });

        Assert.Equal(viewerId, dto.ApplicationUserId);
        Assert.Equal(ownerId, dto.OwnerId);
        Assert.Equal(9, dto.PageSize);
    }

    [Fact]
    public void RecipeUpdatePreservesIdentityAndRelationships()
    {
        var mapper = CreateMapper();
        var recipeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var step = new RecipeStep { Name = "Original" };
        var recipe = new Recipe
        {
            Id = recipeId, ApplicationUserId = ownerId, Name = "Original",
            RecipeSteps = [step], CreationDate = DateTime.UtcNow
        };
        var request = new UpdateRecipeRequestModel
        {
            Name = "Updated", Description = "Details", CategoryId = Guid.NewGuid(), CookingTimeMinutes = 25
        };
        var input = mapper.Map<UpdateRecipeDto>(request, options =>
        {
            options.Items["RecipeId"] = recipeId;
            options.Items["ApplicationUserId"] = ownerId;
        });

        mapper.Map(input, recipe);

        Assert.Equal(recipeId, input.Id);
        Assert.Equal(ownerId, input.ApplicationUserId);
        Assert.Equal("Updated", recipe.Name);
        Assert.Equal(recipeId, recipe.Id);
        Assert.Equal(ownerId, recipe.ApplicationUserId);
        Assert.Same(step, recipe.RecipeSteps.Single());
    }

    [Fact]
    public void CommentMapsContentAndUserName()
    {
        var mapper = CreateMapper();
        var source = new CreateCommentDto { UserId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), Content = " hello " };
        var comment = mapper.Map<Comment>(source);
        var dto = mapper.Map<CommentDto>(comment, options => options.Items["UserName"] = "Author");

        Assert.Equal("hello", comment.Content);
        Assert.Equal(source.UserId, comment.UserId);
        Assert.Equal(source.RecipeId, comment.RecipeId);
        Assert.Equal("Author", dto.UserName);
        Assert.NotNull(dto.UserImageUrl);
    }

    [Fact]
    public void CommentWithoutUserNameUsesEmptyFallback()
    {
        var mapper = CreateMapper();
        var dto = mapper.Map<CommentDto>(new Comment(), options => options.Items["UserName"] = null);

        Assert.Equal(string.Empty, dto.UserName);
    }

    [Fact]
    public void RecipeMappingKeepsViewerFlagsAndMedia()
    {
        var mapper = CreateMapper();
        var viewerId = Guid.NewGuid();
        var recipe = new Recipe
        {
            ApplicationUserId = Guid.NewGuid(), Name = "Recipe", FilePath = "recipes/photo.jpg",
            ApplicationUser = new ApplicationUser { Name = "Chef", Followers = [new UserFollower { FollowerId = viewerId }] },
            ApplicationUserRecipes = [new ApplicationUserRecipe { IsLiked = true, IsSaved = true, IsSavedAt = DateTime.UtcNow }]
        };

        var dto = mapper.Map<RecipeDto>(recipe, options => options.Items["ViewerId"] = viewerId);

        Assert.Equal("Chef", dto.UserName);
        Assert.True(dto.IsFollowed);
        Assert.True(dto.IsLiked);
        Assert.True(dto.IsSaved);
        Assert.NotNull(dto.IsSavedAt);
        Assert.NotNull(dto.FileUrl);
        Assert.Equal(recipe.ApplicationUserId, dto.UserId);
    }

    [Fact]
    public void RecipeDetailsPreserveOrderAndTranslatedIngredients()
    {
        var mapper = CreateMapper();
        var ingredientId = Guid.NewGuid();
        var recipe = new Recipe
        {
            Name = "Recipe",
            RecipeSteps =
            [
                new RecipeStep { Name = "Second", Position = 2 },
                new RecipeStep { Name = "First", Position = 1,
                    Files = [new RecipeStepFile { FilePath = "recipes/step.jpg" }] }
            ],
            RecipeIngredients =
            [
                new RecipeIngredient
                {
                    IngredientId = ingredientId, Quantity = 5,
                    Ingredient = new Ingredient
                    {
                        Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Apple" }]
                    }
                }
            ]
        };

        var dto = mapper.Map<RecipeDetailsDto>(recipe, options =>
        {
            options.Items["ViewerId"] = null;
            options.Items["LanguageCode"] = "uk";
        });

        Assert.Equal("First", dto.Steps.First().Name);
        Assert.Equal("recipes/step.jpg", dto.Steps.First().FileUrls.Single());
        Assert.Equal("Apple", dto.Ingredients.Single().Name);
        Assert.Equal(ingredientId, dto.Ingredients.Single().Id);
        Assert.Equal("uk", dto.Ingredients.Single().Unit);
    }

    [Fact]
    public void UserDtoUsesFallbacksAndImageUrl()
    {
        var mapper = CreateMapper();
        var dto = mapper.Map<UserDto>(new ApplicationUser { Name = "Chef" });

        Assert.Equal("Chef", dto.Name);
        Assert.Equal(string.Empty, dto.Email);
        Assert.Equal(string.Empty, dto.Description);
        Assert.NotNull(dto.ImageUrl);
    }

    private sealed class TestFileService : IFileService
    {
        public bool IsValidSize(Stream fileStream) => true;
        public Task SaveFile(Stream fileStream, string path) => Task.CompletedTask;
        public string? GetFileUrl(string path) => path;
        public bool IsValidMedia(string fileName, long length) => true;
        public void DeleteFile(string path) { }
    }

    private sealed class TestUnitFormatter : IIngredientUnitFormatter
    {
        public string Format(IngredientUnit unit, string languageCode) => languageCode;
    }
}
