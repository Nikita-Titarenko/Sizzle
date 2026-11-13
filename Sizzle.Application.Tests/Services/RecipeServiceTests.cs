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
using Sizzle.Application.Services.Subscriptions;
using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class RecipeServiceTests
{
    [Fact]
    public async Task RecipeSystemPrompt_IsEmbeddedWithSingleLanguageInstructions()
    {
        await using var stream = typeof(RecipeService).Assembly.GetManifestResourceStream("Sizzle.Application.Prompts.RecipeGeneration.md");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var prompt = await reader.ReadToEndAsync();
        Assert.Contains("ONLY in this language", prompt);
        Assert.Contains("Do not provide translations", prompt);
        Assert.Contains("current recipe", prompt);
    }

    [Fact]
    public async Task VideoSystemPrompt_IsEmbeddedWithVideoInstructions()
    {
        await using var stream = typeof(RecipeService).Assembly.GetManifestResourceStream("Sizzle.Application.Prompts.RecipeVideoGeneration.md");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var prompt = await reader.ReadToEndAsync();
        Assert.Contains("cooking video alone", prompt);
        Assert.Contains("two-sentence summary", prompt);
    }

    [Fact]
    public async Task ImageSystemPrompt_IsEmbeddedWithImageInstructions()
    {
        await using var stream = typeof(RecipeService).Assembly.GetManifestResourceStream("Sizzle.Application.Prompts.RecipeImageGeneration.md");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var prompt = await reader.ReadToEndAsync();
        Assert.Contains("photorealistic food photograph", prompt);
    }

    private readonly IRecipeRepository recipes = Substitute.For<IRecipeRepository>();
    private readonly IIngredientRepository ingredients = Substitute.For<IIngredientRepository>();
    private readonly ICategoryRepository categories = Substitute.For<ICategoryRepository>();
    private readonly IUserFollowerRepository followers = Substitute.For<IUserFollowerRepository>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileService files = Substitute.For<IFileService>();
    private readonly IRecipeNotificationSender notifications = Substitute.For<IRecipeNotificationSender>();
    private readonly IRecipeCreatedEventQueue events = Substitute.For<IRecipeCreatedEventQueue>();
    private readonly IIngredientUnitFormatter formatter = Substitute.For<IIngredientUnitFormatter>();
    private readonly IRecipeReadCache cache = Substitute.For<IRecipeReadCache>();
    private readonly IFollowingFeedCache followingCache = Substitute.For<IFollowingFeedCache>();
    private readonly IRecipeInteractionBuffer interactions = Substitute.For<IRecipeInteractionBuffer>();
    private readonly IIngredientReadCache ingredientCache = Substitute.For<IIngredientReadCache>();
    private readonly ICommentReadCache commentCache = Substitute.For<ICommentReadCache>();
    private readonly IMapper mapper = Substitute.For<IMapper>();
    private readonly IAIService ai = Substitute.For<IAIService>();
    private readonly IAIVideoStorage videoStorage = Substitute.For<IAIVideoStorage>();
    private readonly ISubscriptionService subscriptions = Substitute.For<ISubscriptionService>();
    private readonly Guid userId = Guid.NewGuid();

    private RecipeService CreateService(int cooldownMinutes = 10, MediaOptions? mediaOptions = null, bool hasSubscription = true)
    {
        subscriptions.GetStatusAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new SubscriptionStatusDto(hasSubscription ? 60 : 0, []));
        videoStorage.UploadAsync(Arg.Any<RecipeMediaUploadDto>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("gs://test-bucket/recipe-generation/cook.mp4");
        return new RecipeService(recipes, ingredients, categories, followers, unitOfWork, files, notifications, events,
            Microsoft.Extensions.Options.Options.Create(new BusinessLogicOptions { RecipeViewUpdateIntervalMinutes = cooldownMinutes }),
            Microsoft.Extensions.Options.Options.Create(new RecipeCacheOptions()), formatter,
            NullLogger<PersonalizedRecipesLog>.Instance, Microsoft.Extensions.Options.Options.Create(new RecipeLoggingOptions()), cache,
            followingCache, interactions, ingredientCache, commentCache, mapper, NullLogger<RecipeService>.Instance, ai, videoStorage,
            Microsoft.Extensions.Options.Options.Create(mediaOptions ?? new MediaOptions { VideoExtensions = [".mp4", ".webm", ".mov", ".avi", ".mkv"] }),
            subscriptions);
    }

    private async Task<IAsyncEnumerable<RecipeGenerationEvent>> GenerateVideoAsync(GenerateRecipeFromVideoRequestDto request,
        MediaOptions? mediaOptions = null)
    {
        var result = await CreateService(mediaOptions: mediaOptions).GenerateRecipeFromVideoAsync(request, userId);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public async Task GenerateRecipeImageAsync_RejectsMissingRecipeWithoutCallingAI()
    {
        var result = await CreateService().GenerateRecipeImageAsync(null, userId);

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await ai.DidNotReceiveWithAnyArgs().GenerateImageAsync(default!);
    }

    [Fact]
    public async Task GenerateRecipeImageAsync_ReturnsImageFromAIWithoutSavingRecipe()
    {
        var image = new GeneratedRecipeImage([1, 2, 3], "image/png");
        ai.GenerateImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(image);

        var result = await CreateService().GenerateRecipeImageAsync(new GeneratedRecipeDto
        {
            Name = "Pasta", Description = "With tomato sauce",
            Ingredients = [new GeneratedRecipeIngredientDto { Name = "Tomato", Quantity = 2 }]
        }, userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(image.Data, result.Value?.Data);
        Assert.Equal(image.MimeType, result.Value?.MimeType);
        Assert.True(result.Value?.IsAIGeneratedImage);
        await ai.Received(1).GenerateImageAsync(Arg.Is<string>(prompt => prompt.Contains("photorealistic food photograph")
            && prompt.Contains("Pasta") && prompt.Contains("Tomato")),
            Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task GenerateRecipeImageAsync_RejectsUserWithoutSubscriptionBeforeCallingAI()
    {
        var result = await CreateService(hasSubscription: false).GenerateRecipeImageAsync(new GeneratedRecipeDto { Name = "Pasta" }, userId);

        Assert.Equal(ErrorKey.Forbidden, Assert.Single(result.Errors).Key);
        await ai.DidNotReceiveWithAnyArgs().GenerateImageAsync(default!);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_RejectsUserWithoutSubscriptionBeforeUploading()
    {
        var result = await CreateService(hasSubscription: false).GenerateRecipeFromVideoAsync(
            VideoRequest(new RecipeMediaUploadDto { FileName = "cook.mp4", Length = 42, OpenReadStream = () => new MemoryStream() }), userId);

        Assert.Equal(ErrorKey.Forbidden, Assert.Single(result.Errors).Key);
        await videoStorage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_RejectsImageWithoutCallingAI()
    {
        var result = await CollectAsync(await GenerateVideoAsync(VideoRequest(new RecipeMediaUploadDto
        {
            FileName = "photo.jpg", Length = 42, OpenReadStream = () => new MemoryStream()
        })));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        ai.DidNotReceiveWithAnyArgs().GenerateStreamAsync(default!, default!, default);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_RejectsVideoOutsideConfiguredExtensions()
    {
        files.IsValidMedia("cook.webm", 42).Returns(true);
        var result = await CollectAsync(await GenerateVideoAsync(VideoRequest(new RecipeMediaUploadDto
            {
                FileName = "cook.webm", Length = 42, OpenReadStream = () => new MemoryStream()
            }), new MediaOptions { VideoExtensions = [".mp4"] }));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        ai.DidNotReceiveWithAnyArgs().GenerateStreamAsync(default!, default!, default);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_UsesRequestedLanguageAndReusesCatalogWithoutWriting()
    {
        var cheese = new Ingredient
        {
            Id = Guid.NewGuid(), Unit = IngredientUnit.Grams,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Cheese" },
                new IngredientTranslation { LanguageCode = "uk", Name = "Сир" }]
        };
        var feta = new Ingredient
        {
            Id = Guid.NewGuid(), ParentId = cheese.Id, Unit = IngredientUnit.Grams,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Feta" },
                new IngredientTranslation { LanguageCode = "uk", Name = "Фета" }]
        };
        var category = new Category { Id = Guid.NewGuid(), Translations = [new CategoryTranslation { LanguageCode = "en", Name = "Dinner" }] };
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([cheese, feta]);
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([category]);
        files.IsValidMedia("cook.mp4", 42).Returns(true);
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(StreamChunks(JsonSerializer.Serialize(new GenerateRecipeResponseDto { Message = "Ось рецепт", Recipe = new GeneratedRecipeDto
            {
                Name = "Тост із фетою", Description = "Тост із сиром", CookingTimeMinutes = 10, CategoryId = category.Id,
                Steps = [new GeneratedRecipeStepDto { Name = "Підсмажити", Description = "Підсмажити хліб" }],
                Ingredients =
                [
                    new GeneratedRecipeIngredientDto { Name = "Фета", Quantity = 100, Unit = IngredientUnit.Grams },
                    new GeneratedRecipeIngredientDto { Name = "Хліб", Quantity = 2, Unit = IngredientUnit.Pieces }
                ]
            } })));

        var result = await CollectAsync(await GenerateVideoAsync(VideoRequest(new RecipeMediaUploadDto
        {
            FileName = "cook.mp4", Length = 42, OpenReadStream = () => new MemoryStream()
        }, "uk")));

        Assert.True(result.IsSuccess);
        Assert.Equal(feta.Id, result.Value!.Recipe.Ingredients[0].Id);
        Assert.Equal(cheese.Id, result.Value.Recipe.Ingredients[0].ParentId);
        Assert.Null(result.Value.Recipe.Ingredients[1].Id);
        Assert.Equal("Хліб", result.Value.Recipe.Ingredients[1].Name);
        Assert.Equal("Тост із фетою", result.Value.Recipe.Name);
        ai.Received(1).GenerateStreamAsync(Arg.Is<string>(prompt => prompt.Contains("cooking video alone")),
            Arg.Is<IReadOnlyList<AiMessageDto>>(messages => messages.Count == 1 && messages[0].Parts.Count == 2 &&
                messages[0].Parts[0].FileUri!.StartsWith("gs://") && messages[0].Parts[1].Text!.Contains("\"languageCode\":\"uk\"")),
            Arg.Any<CancellationToken>());
        await videoStorage.Received(1).DeleteAsync(Arg.Any<string>(), CancellationToken.None);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await files.DidNotReceiveWithAnyArgs().SaveFile(default!, default!);
    }

    [Fact]
    public async Task GenerateRecipeAsync_TextOnlyCreatesRecipeWithoutPersistence()
    {
        var category = new Category { Id = Guid.NewGuid(), Translations = [new CategoryTranslation { LanguageCode = "en", Name = "Breakfast" }] };
        var ingredient = new Ingredient { Id = Guid.NewGuid(), Unit = IngredientUnit.Pieces,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Egg" }] };
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([category]);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([ingredient]);
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(StreamChunks(JsonSerializer.Serialize(ExampleResponse(category.Id, ingredient.Id))));

        var result = await CollectAsync(CreateService().GenerateRecipeAsync(new GenerateRecipeRequestDto
        {
            Message = "Make eggs in five minutes"
        }));

        Assert.True(result.IsSuccess);
        Assert.Equal("Eggs", result.Value!.Recipe.Name);
        Assert.Equal("Enjoy your eggs", result.Value.Message);
        ai.Received(1).GenerateStreamAsync(Arg.Is<string>(prompt => prompt.Contains("current recipe")),
            Arg.Is<IReadOnlyList<AiMessageDto>>(messages => messages.Count == 1 && messages[0].Parts.Count == 1 &&
                messages[0].Parts[0].Text!.Contains("Make eggs in five minutes") && messages[0].Parts[0].Text!.Contains("Current recipe: null")),
            Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRecipeAsync_SendsCurrentRecipeAndLatestMessageOnly()
    {
        var category = new Category { Id = Guid.NewGuid(), Translations = [new CategoryTranslation { LanguageCode = "en", Name = "Breakfast" }] };
        var ingredient = new Ingredient { Id = Guid.NewGuid(), Unit = IngredientUnit.Pieces,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Egg" }] };
        var current = ExampleResponse(category.Id, ingredient.Id).Recipe;
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([category]);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([ingredient]);
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(StreamChunks(JsonSerializer.Serialize(ExampleResponse(category.Id, ingredient.Id))));

        var result = await CollectAsync(CreateService().GenerateRecipeAsync(new GenerateRecipeRequestDto
        {
            Recipe = current, Message = "Add cheese"
        }));

        Assert.True(result.IsSuccess);
        ai.Received(1).GenerateStreamAsync(Arg.Is<string>(prompt => prompt.Contains("current recipe")),
            Arg.Is<IReadOnlyList<AiMessageDto>>(messages => messages.Count == 1 && messages[0].Parts.Count == 1 &&
                messages[0].Parts[0].Text!.Contains("Add cheese") && messages[0].Parts[0].Text!.Contains("\"name\":\"Eggs\"") &&
                messages[0].Parts[0].FileUri == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRecipeAsync_StreamsChunksBeforeValidatedResult()
    {
        var category = new Category { Id = Guid.NewGuid() };
        var ingredient = new Ingredient { Id = Guid.NewGuid(), Unit = IngredientUnit.Pieces };
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([category]);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([ingredient]);
        var json = JsonSerializer.Serialize(ExampleResponse(category.Id, ingredient.Id));
        var midpoint = json.Length / 2;
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(StreamChunks(json[..midpoint], json[midpoint..]));

        var events = new List<RecipeGenerationEvent>();
        await foreach (var item in CreateService().GenerateRecipeAsync(new GenerateRecipeRequestDto { Message = "Make eggs" }))
            events.Add(item);

        Assert.Equal(["chunk", "chunk", "completed"], events.Select(item => item.Type));
        Assert.Equal(json, string.Concat(events.Take(2).Select(item => item.Text)));
        Assert.Equal("Eggs", events[2].Result!.Recipe.Name);
        Assert.Equal(ingredient.Id, events[2].Result.Recipe.Ingredients[0].Id);
    }

    [Fact]
    public async Task GenerateRecipeAsync_InvalidCompletedRecipeEmitsErrorInsteadOfCompleted()
    {
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(StreamChunks("{}"));

        var events = new List<RecipeGenerationEvent>();
        await foreach (var item in CreateService().GenerateRecipeAsync(new GenerateRecipeRequestDto { Message = "Make eggs" }))
            events.Add(item);

        Assert.Equal(["chunk", "error"], events.Select(item => item.Type));
        Assert.Equal(ErrorKey.UnexpectedError, Assert.Single(events[1].Errors!).Key);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_DoesNotSaveVideoOrRecipe()
    {
        var category = new Category { Id = Guid.NewGuid(), Translations = [new CategoryTranslation { LanguageCode = "en", Name = "Breakfast" }] };
        var ingredient = new Ingredient { Id = Guid.NewGuid(), Unit = IngredientUnit.Pieces,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Egg" }] };
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([category]);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([ingredient]);
        files.IsValidMedia("cook.mp4", 42).Returns(true);
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(StreamChunks(JsonSerializer.Serialize(ExampleResponse(category.Id, ingredient.Id))));

        var result = await CollectAsync(await GenerateVideoAsync(VideoRequest(new RecipeMediaUploadDto
        { FileName = "cook.mp4", Length = 42, OpenReadStream = () => new MemoryStream() })));

        Assert.True(result.IsSuccess);
        await files.DidNotReceiveWithAnyArgs().SaveFile(default!, default!);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await videoStorage.Received(1).DeleteAsync(Arg.Any<string>(), CancellationToken.None);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_DeletesTemporaryVideoWhenAiFails()
    {
        files.IsValidMedia("cook.mp4", 42).Returns(true);
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(FailingStream());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await CollectAsync(await GenerateVideoAsync(
            VideoRequest(new RecipeMediaUploadDto { FileName = "cook.mp4", Length = 42, OpenReadStream = () => new MemoryStream() }))));

        await videoStorage.Received(1).DeleteAsync("gs://test-bucket/recipe-generation/cook.mp4", CancellationToken.None);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_DeletesTemporaryVideoWhenConsumerStopsEarly()
    {
        files.IsValidMedia("cook.mp4", 42).Returns(true);
        categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        ai.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(StreamChunks("{", "}"));

        await foreach (var item in await GenerateVideoAsync(VideoRequest(new RecipeMediaUploadDto
        { FileName = "cook.mp4", Length = 42, OpenReadStream = () => new MemoryStream() })))
        {
            Assert.Equal("chunk", item.Type);
            break;
        }

        await videoStorage.Received(1).DeleteAsync("gs://test-bucket/recipe-generation/cook.mp4", CancellationToken.None);
    }

    [Fact]
    public async Task GenerateRecipeFromVideoAsync_RejectsMissingLanguageBeforeUpload()
    {
        files.IsValidMedia("cook.mp4", 42).Returns(true);

        var result = await CollectAsync(await GenerateVideoAsync(
            VideoRequest(new RecipeMediaUploadDto { FileName = "cook.mp4", Length = 42, OpenReadStream = () => new MemoryStream() }, " ")));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await videoStorage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 1, 1)]
    [InlineData(true, 0, 2)]
    public async Task GenerateRecipeFromVideoAsync_RejectsMissingVideoOrExtraMultipartData(bool hasVideo, int formFieldCount, int fileCount)
    {
        var video = hasVideo
            ? new RecipeMediaUploadDto { FileName = "cook.mp4", Length = 42, OpenReadStream = () => new MemoryStream() }
            : null;

        var result = await CollectAsync(await GenerateVideoAsync(VideoRequest(video, formFieldCount: formFieldCount, fileCount: fileCount)));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        await videoStorage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default);
        ai.DidNotReceiveWithAnyArgs().GenerateStreamAsync(default!, default!, default);
    }

    private static GenerateRecipeFromVideoRequestDto VideoRequest(RecipeMediaUploadDto? video, string languageCode = "en",
        int formFieldCount = 0, int fileCount = 1) => new()
    {
        Video = video, LanguageCode = languageCode, FormFieldCount = formFieldCount, FileCount = fileCount
    };

    private static async Task<Result<GenerateRecipeResponseDto>> CollectAsync(IAsyncEnumerable<RecipeGenerationEvent> stream)
    {
        var events = new List<RecipeGenerationEvent>();
        await foreach (var item in stream) events.Add(item);
        var completed = events.SingleOrDefault(item => item.Type == "completed");
        return completed == null
            ? new Result<GenerateRecipeResponseDto> { Errors = events.Where(item => item.Type == "error").SelectMany(item => item.Errors ?? []).ToList() }
            : new Result<GenerateRecipeResponseDto> { Value = completed.Result };
    }

    private static async IAsyncEnumerable<string> StreamChunks(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<string> FailingStream()
    {
        await Task.Yield();
        yield return "{";
        throw new InvalidOperationException("AI unavailable");
    }

    [Fact]
    public async Task GenerateRecipeAsync_RejectsEmptyMessageBeforeCallingAi()
    {
        var result = await CollectAsync(CreateService().GenerateRecipeAsync(new GenerateRecipeRequestDto { Message = " " }));

        Assert.Equal(ErrorKey.BadRequest, Assert.Single(result.Errors).Key);
        ai.DidNotReceiveWithAnyArgs().GenerateStreamAsync(default!, default!, default);
    }

    private static GenerateRecipeResponseDto ExampleResponse(Guid categoryId, Guid ingredientId) => new()
    {
        Message = "Enjoy your eggs",
        Recipe = new GeneratedRecipeDto
        {
            Name = "Eggs", Description = "Cooked eggs", CookingTimeMinutes = 5, CategoryId = categoryId,
            Steps = [new GeneratedRecipeStepDto { Name = "Cook", Description = "Cook eggs" }],
            Ingredients = [new GeneratedRecipeIngredientDto { Id = ingredientId, Name = "Egg", Unit = IngredientUnit.Pieces, Quantity = 2 }]
        }
    };

    [Theory]
    [InlineData("Feta")]
    [InlineData("Фета")]
    [InlineData("  фета  ")]
    [InlineData("Фетта")]
    public async Task CreateRecipeAsync_PersistsNewIngredientTranslations(string inputName)
    {
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([new Ingredient
        {
            Id = Guid.NewGuid(), Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Salt" },
                new IngredientTranslation { LanguageCode = "uk", Name = "Сіль" }]
        }]);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("""{"status":"approved","reason":"ok","newIngredients":[{"index":0,"name":"Feta","calories":265,"translations":{"en":"Feta","uk":"Фета"}}]}""");
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Toast",
            Description = "Toast with cheese", CookingTimeMinutes = 10, IsAIGeneratedImage = true,
            NewIngredients = [new NewIngredientDto
            {
                Name = inputName, Quantity = 100, Unit = IngredientUnit.Grams
            }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetByNamesAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns([]);
        mapper.Map<Recipe>(input).Returns(new Recipe { Name = input.Name, Description = input.Description, CategoryId = input.CategoryId,
            ApplicationUserId = input.ApplicationUserId });

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Null(result.CorrectedRecipe);
        recipes.Received(1).Add(Arg.Is<Recipe>(recipe => recipe.IsAIGeneratedImage));
        ingredients.Received(1).Add(Arg.Is<Ingredient>(ingredient => ingredient.Calories == 265 && ingredient.Translations.Any(translation =>
            translation.LanguageCode == "uk" && translation.Name == "Фета")));
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<AiMessageDto>>(messages =>
            messages.Count == 1 && messages[0].Parts[0].Text != null &&
            messages[0].Parts[0].Text!.Contains("\"locales\":[\"en\",\"uk\"]", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("{\"FR\":\"Feta\",\"pl\":\"Feta\",\"uk\":\"Фета\"}", true)]
    [InlineData("{\"fr\":\"Feta\",\"pl\":\"Feta\"}", false)]
    public async Task CreateRecipeAsync_UsesDatabaseLocalesForNewIngredientTranslations(string translations, bool approved)
    {
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([new Ingredient
        {
            Id = Guid.NewGuid(), Translations = [new IngredientTranslation { LanguageCode = "pl", Name = "Sól" },
                new IngredientTranslation { LanguageCode = "uk", Name = "Сіль" },
                new IngredientTranslation { LanguageCode = "fr", Name = "Sel" }]
        }]);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("{\"status\":\"approved\",\"reason\":\"ok\",\"newIngredients\":[{\"index\":0,\"name\":\"Feta\",\"calories\":265,\"translations\":"
                + translations + "}]}");
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Toast",
            Description = "Toast with cheese", CookingTimeMinutes = 10,
            NewIngredients = [new NewIngredientDto { Name = "Feta", Quantity = 100, Unit = IngredientUnit.Grams }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetByNamesAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns([]);
        mapper.Map<Recipe>(input).Returns(new Recipe { Name = input.Name, Description = input.Description,
            CategoryId = input.CategoryId, ApplicationUserId = input.ApplicationUserId });

        var result = await CreateService().CreateRecipeAsync(input);

        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<AiMessageDto>>(messages =>
            messages.Count == 1 && messages[0].Parts[0].Text != null &&
            messages[0].Parts[0].Text!.Contains("\"locales\":[\"fr\",\"pl\",\"uk\"]", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        if (approved)
        {
            Assert.True(result.IsSuccess);
            ingredients.Received(1).Add(Arg.Is<Ingredient>(ingredient =>
                ingredient.Calories == 265 && ingredient.Translations.Count == 3 &&
                ingredient.Translations.Any(translation => translation.LanguageCode == "fr" && translation.Name == "Feta") &&
                ingredient.Translations.Any(translation => translation.LanguageCode == "pl" && translation.Name == "Feta") &&
                ingredient.Translations.Any(translation => translation.LanguageCode == "uk" && translation.Name == "Фета")));
            await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.Equal(ErrorKey.UnexpectedError, Assert.Single(result.Errors).Key);
            ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
            recipes.DidNotReceive().Add(Arg.Any<Recipe>());
            await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"calories\":-1")]
    public async Task CreateRecipeAsync_InvalidAiCaloriesDoesNotWrite(string caloriesJson)
    {
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([new Ingredient
        {
            Id = Guid.NewGuid(), Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Salt" }]
        }]);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("{\"status\":\"approved\",\"reason\":\"ok\",\"newIngredients\":[{\"index\":0,\"name\":\"Feta\""
                + caloriesJson + ",\"translations\":{\"en\":\"Feta\"}}]}");
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Toast",
            Description = "Cheese toast", CookingTimeMinutes = 10,
            NewIngredients = [new NewIngredientDto { Name = "Feta", Quantity = 100, Unit = IngredientUnit.Grams }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.Equal(ErrorKey.UnexpectedError, Assert.Single(result.Errors).Key);
        ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRecipeAsync_DraftDoesNotCreateIngredientsOrNotifyFollowers()
    {
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Draft",
            Description = "Unfinished", CookingTimeMinutes = 10, IsDraft = true,
            NewIngredients = [new NewIngredientDto { Name = "Cheese", Quantity = 100, Unit = IngredientUnit.Grams }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);
        mapper.Map<Recipe>(input).Returns(new Recipe { ApplicationUserId = input.ApplicationUserId, CategoryId = input.CategoryId });

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.True(result.IsSuccess);
        await ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default!);
        await ingredients.DidNotReceive().GetAllWithTranslationsAsync(Arg.Any<CancellationToken>());
        recipes.Received(1).Add(Arg.Is<Recipe>(recipe => recipe.IsDraft && recipe.RecipeIngredients.Count == 0));
        ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
        await ingredients.DidNotReceive().GetByNamesAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).UpdateListsAsync(Arg.Is<RecipeListMutation>(mutation => mutation.After!.IsDraft), null,
            Arg.Any<CancellationToken>());
        await events.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
        await notifications.DidNotReceiveWithAnyArgs().RecipeCreatedAsync(default, default, default!, default);
    }

    [Fact]
    public async Task GetDraftRecipesAsync_AlwaysUsesAuthenticatedUserAsOwner()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var input = new GetRecipesDto { ApplicationUserId = userId, OwnerId = otherId, PageIndex = 1, PageSize = 10 };
        recipes.GetDraftRecipesAsync(Arg.Any<GetRecipesDto>(), Arg.Any<CancellationToken>())
            .Returns(new PageResponse<Recipe> { PageIndex = 1, PageSize = 10 });

        var result = await CreateService().GetDraftRecipesAsync(input);

        Assert.Empty(result.Data);
        await recipes.Received(1).GetDraftRecipesAsync(Arg.Is<GetRecipesDto>(query =>
            query.OwnerId == userId && query.ApplicationUserId == userId && query.PageIndex == 1), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    public async Task UpdateRecipeAsync_DraftStatusOnlyChangesWhenExplicitlyRequested(bool? requestedStatus, bool expectedDraft)
    {
        if (!expectedDraft)
        {
            ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([new Ingredient
            {
                Id = Guid.NewGuid(), Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Salt" },
                    new IngredientTranslation { LanguageCode = "uk", Name = "Сіль" }]
            }]);
            ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
                .Returns("""{"status":"approved","reason":"ok","newIngredients":[{"index":0,"name":"Cheese","calories":400,"translations":{"en":"Cheese","uk":"Сир"}}]}""");
        }

        var owner = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), ApplicationUserId = owner, IsDraft = true };
        var input = new UpdateRecipeDto
        {
            Id = recipe.Id, ApplicationUserId = owner, CategoryId = Guid.NewGuid(), Name = "Updated",
            Description = "Updated draft", CookingTimeMinutes = 10, IsDraft = requestedStatus,
            NewIngredients = [new NewIngredientDto { Name = "Cheese", Quantity = 100, Unit = IngredientUnit.Grams }]
        };
        recipes.GetWithDetailsAsync(recipe.Id, owner, Arg.Any<CancellationToken>()).Returns(recipe);
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetByNamesAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateService().UpdateRecipeAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedDraft, recipe.IsDraft);
        await cache.Received(1).UpdateListsAsync(Arg.Is<RecipeListMutation>(mutation =>
            mutation.Before!.IsDraft && mutation.After!.IsDraft == expectedDraft), Arg.Any<Func<string, RecipeDetailsDto>>(),
            Arg.Any<CancellationToken>());
        if (expectedDraft)
        {
            await ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default!);
            await ingredients.DidNotReceive().GetAllWithTranslationsAsync(Arg.Any<CancellationToken>());
            ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
            await events.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
        }
        else
        {
            await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>());
            ingredients.Received(1).Add(Arg.Any<Ingredient>());
            ingredients.Received(1).Add(Arg.Is<Ingredient>(ingredient => ingredient.Translations.Any(translation =>
                translation.LanguageCode == "uk" && translation.Name == "Сир")));
            await events.Received(1).EnqueueAsync(Arg.Any<RecipeCreatedEventDto>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateRecipeAsync_RejectedRecipeDoesNotChangeOrPersistExistingRecipe(bool wasDraft)
    {
        var ownerId = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), ApplicationUserId = ownerId, Name = "Original", IsDraft = wasDraft };
        var input = new UpdateRecipeDto
        {
            Id = recipe.Id, ApplicationUserId = ownerId, CategoryId = Guid.NewGuid(), Name = "Joke recipe",
            Description = "Not food", CookingTimeMinutes = 10, IsDraft = false
        };
        recipes.GetWithDetailsAsync(recipe.Id, ownerId, Arg.Any<CancellationToken>()).Returns(recipe);
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("""{"status":"rejected","reason":"Not food"}""");

        var result = await CreateService().UpdateRecipeAsync(input);

        Assert.Equal(ErrorKey.RecipeRejected, Assert.Single(result.Errors).Key);
        Assert.Null(result.CorrectedRecipe);
        Assert.Equal("Original", recipe.Name);
        Assert.Equal(wasDraft, recipe.IsDraft);
        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>());
        ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
        recipes.DidNotReceive().RemoveSteps(Arg.Any<IEnumerable<RecipeStep>>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRecipeAsync_CatalogIngredientCorrectionDoesNotWrite()
    {
        var ownerId = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), ApplicationUserId = ownerId, Name = "Original" };
        var known = new Ingredient
        {
            Id = Guid.NewGuid(), Unit = IngredientUnit.Milliliters,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Milk" }]
        };
        var input = new UpdateRecipeDto
        {
            Id = recipe.Id, ApplicationUserId = ownerId, CategoryId = Guid.NewGuid(), Name = "Porridge",
            Description = "With milk", CookingTimeMinutes = 10,
            NewIngredients = [new NewIngredientDto { Name = "Mlik", Quantity = 200, Unit = IngredientUnit.Milliliters }]
        };
        recipes.GetWithDetailsAsync(recipe.Id, ownerId, Arg.Any<CancellationToken>()).Returns(recipe);
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([known]);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new
            {
                status = "correction", reason = "Mlik means Milk", correctedRecipe = new
                {
                    input.Name, input.Description, input.CookingTimeMinutes, input.CategoryId,
                    steps = Array.Empty<object>(), ingredients = new[]
                    {
                        new { id = known.Id, name = "Milk", parentId = (Guid?)null,
                            unit = "Milliliters", quantity = 200 }
                    }
                }
            }));

        var result = await CreateService().UpdateRecipeAsync(input);

        Assert.Equal(ErrorKey.RecipeNeedsCorrection, Assert.Single(result.Errors).Key);
        Assert.Equal(known.Id, Assert.Single(result.CorrectedRecipe!.Ingredients).Id);
        Assert.Equal("Original", recipe.Name);
        ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
        recipes.DidNotReceive().RemoveSteps(Arg.Any<IEnumerable<RecipeStep>>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRecipeAsync_ReviewsMediaInOneCallAndDeletesTemporaryVideoOnRejection()
    {
        var ownerId = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), ApplicationUserId = ownerId, Name = "Original" };
        var image = new RecipeMediaUploadDto
        {
            FileName = "cover.png", Length = 3, OpenReadStream = () => new MemoryStream([1, 2, 3])
        };
        var video = new RecipeMediaUploadDto
        {
            FileName = "step.mp4", Length = 3, OpenReadStream = () => new MemoryStream([4, 5, 6])
        };
        var input = new UpdateRecipeDto
        {
            Id = recipe.Id, ApplicationUserId = ownerId, CategoryId = Guid.NewGuid(), Name = "Updated",
            Description = "With video", CookingTimeMinutes = 10, File = image,
            Steps = [new RecipeStepInputDto { Name = "Cook", Description = "Heat", Files = [video] }]
        };
        recipes.GetWithDetailsAsync(recipe.Id, ownerId, Arg.Any<CancellationToken>()).Returns(recipe);
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        files.IsValidMedia(Arg.Any<string>(), Arg.Any<long>()).Returns(true);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("""{"status":"rejected","reason":"Unrelated video"}""");

        var result = await CreateService().UpdateRecipeAsync(input);

        Assert.Equal(ErrorKey.RecipeRejected, Assert.Single(result.Errors).Key);
        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<AiMessageDto>>(messages =>
            messages.Count == 1 && messages[0].Parts.Any(part => part.Data != null && part.Data.SequenceEqual(new byte[] { 1, 2, 3 })) &&
            messages[0].Parts.Any(part => part.FileUri == "gs://test-bucket/recipe-generation/cook.mp4")), Arg.Any<CancellationToken>());
        await videoStorage.Received(1).DeleteAsync("gs://test-bucket/recipe-generation/cook.mp4", Arg.Any<CancellationToken>());
        await files.DidNotReceive().SaveFile(Arg.Any<Stream>(), Arg.Any<string>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRecipeAsync_ChangingPublishedRecipeToDraftSkipsReview()
    {
        var ownerId = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), ApplicationUserId = ownerId, Name = "Original" };
        var input = new UpdateRecipeDto
        {
            Id = recipe.Id, ApplicationUserId = ownerId, CategoryId = Guid.NewGuid(), Name = "Draft",
            Description = "Unfinished", CookingTimeMinutes = 10, IsDraft = true
        };
        recipes.GetWithDetailsAsync(recipe.Id, ownerId, Arg.Any<CancellationToken>()).Returns(recipe);
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateService().UpdateRecipeAsync(input);

        Assert.True(result.IsSuccess);
        Assert.True(recipe.IsDraft);
        await ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default!);
        await ingredients.DidNotReceive().GetAllWithTranslationsAsync(Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task UpdateRecipeAsync_ApprovedPublishedRecipeReviewsOnceBeforeSaving(bool? requestedDraft)
    {
        var ownerId = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), ApplicationUserId = ownerId, Name = "Original" };
        var input = new UpdateRecipeDto
        {
            Id = recipe.Id, ApplicationUserId = ownerId, CategoryId = Guid.NewGuid(), Name = "Soup",
            Description = "Vegetable soup", CookingTimeMinutes = 10, IsDraft = requestedDraft
        };
        recipes.GetWithDetailsAsync(recipe.Id, ownerId, Arg.Any<CancellationToken>()).Returns(recipe);
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("""{"status":"approved","reason":"ok","newIngredients":[]}""");

        var result = await CreateService().UpdateRecipeAsync(input);

        Assert.True(result.IsSuccess);
        Assert.False(recipe.IsDraft);
        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

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
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var ingredientId = Guid.NewGuid();
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), Name = "Soup", Description = "Broth", CookingTimeMinutes = 10,
            CategoryId = Guid.NewGuid(), Ingredients = [new RecipeIngredientInputDto { Id = ingredientId, Quantity = 2 }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRecipeAsync_UsesTrackedExistingIngredientForRecipeLink()
    {
        var existing = new Ingredient
        {
            Id = Guid.NewGuid(), Calories = 26,
            Translations = [new IngredientTranslation { LanguageCode = "uk", Name = "Гарбуз" }]
        };
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Суп",
            Description = "Гарбузовий суп", CookingTimeMinutes = 45,
            Ingredients = [new RecipeIngredientInputDto { Id = existing.Id, Quantity = 600 }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([existing]);
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([existing]);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("""{"status":"approved","reason":"ok","newIngredients":[]}""");
        mapper.Map<Recipe>(input).Returns(new Recipe { Name = input.Name, Description = input.Description,
            CategoryId = input.CategoryId, ApplicationUserId = input.ApplicationUserId });

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.True(result.IsSuccess);
        await ingredients.Received(1).GetByIdsWithTranslationsTrackedAsync(
            Arg.Is<Guid[]>(ids => ids.SequenceEqual(new[] { existing.Id })), Arg.Any<CancellationToken>());
        recipes.Received(1).Add(Arg.Is<Recipe>(recipe => recipe.RecipeIngredients.Any(link =>
            link.IngredientId == existing.Id && link.Ingredient == existing && link.Quantity == 600)));
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("rejected", ErrorKey.RecipeRejected)]
    [InlineData("unknown", ErrorKey.UnexpectedError)]
    public async Task CreateRecipeAsync_RejectedOrInvalidReviewDoesNotWrite(string status, ErrorKey expected)
    {
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var input = NewRecipeInput();
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new { status, reason = "Not a recipe" }));

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.Equal(expected, Assert.Single(result.Errors).Key);
        Assert.Null(result.CorrectedRecipe);
        ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRecipeAsync_CorrectionReturnsRecipeWithoutWriting()
    {
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var input = NewRecipeInput();
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new
            {
                status = "correction", reason = "Fix ingredient name", correctedRecipe = new
                {
                    input.Name, input.Description, input.CookingTimeMinutes, input.CategoryId,
                    steps = input.Steps.Select(step => new { step.Name, step.Description }), ingredients = Array.Empty<object>()
                }
            }));

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.Equal(ErrorKey.RecipeNeedsCorrection, Assert.Single(result.Errors).Key);
        Assert.NotNull(result.CorrectedRecipe);
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRecipeAsync_CatalogAliasCorrectionReturnsExistingIngredientId()
    {
        var known = new Ingredient
        {
            Id = Guid.NewGuid(), Unit = IngredientUnit.Milliliters,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Milk" }]
        };
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([known]);
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Porridge",
            Description = "Oatmeal with milk", CookingTimeMinutes = 10,
            NewIngredients = [new NewIngredientDto { Name = "Mlik", Quantity = 200, Unit = IngredientUnit.Milliliters }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new
            {
                status = "correction", reason = "Mlik means Milk", correctedRecipe = new
                {
                    input.Name, input.Description, input.CookingTimeMinutes, input.CategoryId,
                    steps = Array.Empty<object>(), ingredients = new[]
                    {
                        new { id = known.Id, name = "Milk", parentId = (Guid?)null,
                            unit = "Milliliters", quantity = 200 }
                    }
                }
            }));

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.Equal(ErrorKey.RecipeNeedsCorrection, Assert.Single(result.Errors).Key);
        Assert.Equal(known.Id, Assert.Single(result.CorrectedRecipe!.Ingredients).Id);
        ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRecipeAsync_ApprovedExistingCatalogNameUsesExistingIngredient()
    {
        var known = new Ingredient
        {
            Id = Guid.NewGuid(), Unit = IngredientUnit.Milliliters,
            Translations = [new IngredientTranslation { LanguageCode = "en", Name = "Milk" },
                new IngredientTranslation { LanguageCode = "uk", Name = "Молоко" }]
        };
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([known]);
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Porridge",
            Description = "Oatmeal with milk", CookingTimeMinutes = 10,
            NewIngredients = [new NewIngredientDto { Name = "Milk", Quantity = 200, Unit = IngredientUnit.Milliliters }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("""{"status":"approved","reason":"ok","newIngredients":[{"index":0,"name":"Milk","calories":60,"translations":{"en":"Milk","uk":"Молоко"}}]}""");
        ingredients.GetByIdsWithTranslationsTrackedAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns([]);
        ingredients.GetByNamesAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns([known]);
        mapper.Map<Recipe>(input).Returns(new Recipe { Name = input.Name, Description = input.Description,
            CategoryId = input.CategoryId, ApplicationUserId = input.ApplicationUserId });

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Null(result.CorrectedRecipe);
        ingredients.DidNotReceive().Add(Arg.Any<Ingredient>());
        recipes.Received(1).Add(Arg.Is<Recipe>(recipe => recipe.RecipeIngredients.Any(link =>
            link.IngredientId == known.Id && link.Quantity == 200)));
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRecipeAsync_ReviewsMediaInOneCallAndDeletesTemporaryVideoOnRejection()
    {
        ingredients.GetAllWithTranslationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var image = new RecipeMediaUploadDto
        {
            FileName = "cover.png", Length = 3, OpenReadStream = () => new MemoryStream([1, 2, 3])
        };
        var video = new RecipeMediaUploadDto
        {
            FileName = "step.mp4", Length = 3, OpenReadStream = () => new MemoryStream([4, 5, 6])
        };
        var input = new CreateRecipeDto
        {
            ApplicationUserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Soup",
            Description = "Broth", CookingTimeMinutes = 10, File = image,
            Steps = [new RecipeStepInputDto { Name = "Cook", Description = "Heat", Files = [video] }]
        };
        categories.ExistsAsync(input.CategoryId, Arg.Any<CancellationToken>()).Returns(true);
        files.IsValidMedia(Arg.Any<string>(), Arg.Any<long>()).Returns(true);
        ai.GenerateJsonAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AiMessageDto>>(), Arg.Any<CancellationToken>())
            .Returns("""{"status":"rejected","reason":"Unrelated video"}""");

        var result = await CreateService().CreateRecipeAsync(input);

        Assert.Equal(ErrorKey.RecipeRejected, Assert.Single(result.Errors).Key);
        await ai.Received(1).GenerateJsonAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<AiMessageDto>>(messages =>
            messages.Count == 1 && messages[0].Parts.Any(part => part.Data != null && part.Data.SequenceEqual(new byte[] { 1, 2, 3 })) &&
            messages[0].Parts.Any(part => part.FileUri == "gs://test-bucket/recipe-generation/cook.mp4")), Arg.Any<CancellationToken>());
        await videoStorage.Received(1).DeleteAsync("gs://test-bucket/recipe-generation/cook.mp4", Arg.Any<CancellationToken>());
        recipes.DidNotReceive().Add(Arg.Any<Recipe>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteRecipeAsync_RejectsOtherOwnerWithoutRemovingOrInvalidating()
    {
        var id = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var user = Guid.NewGuid();
        recipes.GetWithDetailsAsync(id, user, Arg.Any<CancellationToken>())
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
        recipes.GetWithDetailsAsync(recipe.Id, owner, source.Token).Returns(recipe);

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

        var result = await CreateService().UpdateViewAsync(new UpdateRecipeViewDto { RecipeId = recipeId, ApplicationUserId = userId });

        Assert.True(result.IsSuccess);
        Assert.Equal(5, recipe.ViewCount);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await recipes.DidNotReceive().GetWithAuthorAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateViewAsync_BufferedViewDoesNotFetchRecipeForResponse()
    {
        var recipeId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        interactions.Enabled.Returns(true);
        interactions.UpdateViewAsync(viewerId, recipeId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().UpdateViewAsync(new UpdateRecipeViewDto
        {
            RecipeId = recipeId, ApplicationUserId = viewerId
        });

        Assert.True(result.IsSuccess);
        await recipes.DidNotReceive().GetWithAuthorAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().GetAsync<Result<RecipeDetailsDto>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateViewAsync_BufferedMissingRecipeReturnsNotFound()
    {
        var recipeId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        interactions.Enabled.Returns(true);
        interactions.UpdateViewAsync(viewerId, recipeId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateService().UpdateViewAsync(new UpdateRecipeViewDto
        {
            RecipeId = recipeId, ApplicationUserId = viewerId
        });

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await recipes.DidNotReceive().GetWithAuthorAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateViewAsync_MissingRecipeStillReturnsNotFound()
    {
        var recipeId = Guid.NewGuid();

        var result = await CreateService().UpdateViewAsync(new UpdateRecipeViewDto
        {
            RecipeId = recipeId, ApplicationUserId = Guid.NewGuid()
        });

        Assert.Equal(ErrorKey.NotFound, Assert.Single(result.Errors).Key);
        await recipes.DidNotReceive().GetWithAuthorAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
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
