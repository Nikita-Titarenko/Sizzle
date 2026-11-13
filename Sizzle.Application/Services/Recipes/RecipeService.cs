using AutoMapper;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Options;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Application.Services.Comments;
using Sizzle.Application.Services.Subscriptions;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Recipes;

public class RecipeService(IRecipeRepository recipeRepository, IIngredientRepository ingredientRepository, ICategoryRepository categoryRepository, IUserFollowerRepository followerRepository, IUnitOfWork unitOfWork, IFileService fileService, IRecipeNotificationSender notificationSender, IRecipeCreatedEventQueue eventQueue, IOptions<BusinessLogicOptions> businessLogicOptions, IOptions<RecipeCacheOptions> cacheOptions, IIngredientUnitFormatter unitFormatter, ILogger<PersonalizedRecipesLog> logger, IOptions<RecipeLoggingOptions> recipeLoggingOptions, IRecipeReadCache readCache, IFollowingFeedCache followingFeedCache, IRecipeInteractionBuffer interactionsBuffer, IIngredientReadCache ingredientCache, ICommentReadCache commentCache, IMapper mapper, ILogger<RecipeService> operationLogger, IAIService aiService, IAIVideoStorage videoStorage, IOptions<MediaOptions> mediaOptions, ISubscriptionService subscriptionService) : IRecipeService
{
    private const int LikedInteractionWeight = 3;
    private const int SavedInteractionWeight = 6;
    private const int ShareInteractionWeight = 10;
    private static readonly JsonSerializerOptions AiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly Lazy<string> RecipeSystemPrompt = new(() => LoadRecipeSystemPrompt("RecipeGeneration.md"));
    private static readonly Lazy<string> VideoSystemPrompt = new(() => LoadRecipeSystemPrompt("RecipeVideoGeneration.md"));
    private static readonly Lazy<string> ImageSystemPrompt = new(() => LoadRecipeSystemPrompt("RecipeImageGeneration.md"));
    private static readonly Lazy<string> CreationReviewPrompt = new(() => LoadRecipeSystemPrompt("RecipeCreationReview.md"));

    public async Task<PageResponse<FeedRecipeDto>> GetPersonalizedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        var pendingIds = interactionsBuffer.Enabled
            ? (await interactionsBuffer.GetPendingInteractionsAsync(dto.ApplicationUserId!.Value, cancellationToken))
                .Select(interaction => interaction.RecipeId).Distinct().Order().ToArray()
            : [];
        var seen = pendingIds.ToHashSet();
        var offset = (long)dto.PageIndex * dto.PageSize;
        var required = offset + dto.PageSize;
        var cached = await readCache.GetOrRefreshAsync<PageResponse<FeedRecipeDto>>(
            $"recipes:personalized:feed:v2:{JsonSerializer.Serialize(new { dto.ApplicationUserId, dto.CategoryIds, dto.IngredientIds, dto.PageSize })}",
            feed =>
            {
                var unseen = feed.Data.Count(recipe => !seen.Contains(recipe.Id));
                return unseen >= required || unseen >= feed.TotalSize - feed.Data.Count(recipe => seen.Contains(recipe.Id));
            },
            (previous, token) => GetPersonalizedRecipesCoreAsync(dto, pendingIds,
                (int)Math.Min(int.MaxValue, Math.Max(required * 2, (long)(previous?.Data.Count ?? 0) + dto.PageSize * 2L)), token), cancellationToken);
        var available = cached.Data.Where(recipe => !seen.Contains(recipe.Id)).ToArray();
        var page = new PageResponse<FeedRecipeDto>
        {
            Data = offset >= available.Length ? [] : available.Skip((int)offset).Take(dto.PageSize).ToArray(),
            TotalSize = Math.Max(available.Length, cached.TotalSize - (cached.Data.Count - available.Length)),
            PageIndex = dto.PageIndex,
            PageSize = dto.PageSize
        };
        await interactionsBuffer.ApplyAsync(page.Data, dto.ApplicationUserId, cancellationToken);
        return page;
    }

    private async Task<PageResponse<FeedRecipeDto>> GetPersonalizedRecipesCoreAsync(GetRecipesDto dto, Guid[] pendingIds, int batchSize, CancellationToken cancellationToken)
    {
        const int authorMultiplier = 4;
        const int categoryMultiplier = 3;
        const int ingredientMultiplier = 2;
        const int nameMultiplier = 5;

        var userId = dto.ApplicationUserId!.Value;
        var interactions = await recipeRepository.GetRecentInteractionsAsync(userId, 100, cancellationToken);
        var candidates = await recipeRepository.GetRecommendationCandidatesAsync(dto, cancellationToken);
        var seenIds = interactions.Select(interaction => interaction.Recipe.Id).Concat(pendingIds).ToHashSet();
        var authorWeights = new Dictionary<Guid, int>();
        var categoryWeights = new Dictionary<Guid, int>();
        var ingredientWeights = new Dictionary<Guid, int>();
        var wordWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var interaction in interactions)
        {
            var weight = InteractionWeight(interaction);
            authorWeights[interaction.Recipe.AuthorId] = authorWeights.GetValueOrDefault(interaction.Recipe.AuthorId) + weight;
            categoryWeights[interaction.Recipe.CategoryId] = categoryWeights.GetValueOrDefault(interaction.Recipe.CategoryId) + weight;
            foreach (var id in interaction.Recipe.IngredientIds.Distinct())
            {
                ingredientWeights[id] = ingredientWeights.GetValueOrDefault(id) + weight;
            }

            foreach (var word in NameWords(interaction.Recipe.Name))
            {
                wordWeights[word] = wordWeights.GetValueOrDefault(word) + weight;
            }
        }

        var scored = candidates.Where(recipe => !seenIds.Contains(recipe.Id))
            .Select(recipe => new
            {
                Recipe = recipe,
                AuthorScore = authorMultiplier * authorWeights.GetValueOrDefault(recipe.AuthorId),
                CategoryScore = categoryMultiplier * categoryWeights.GetValueOrDefault(recipe.CategoryId),
                IngredientScore = ingredientMultiplier * recipe.IngredientIds.Distinct().Sum(id => ingredientWeights.GetValueOrDefault(id)),
                NameScore = nameMultiplier * NameWords(recipe.Name).Sum(word => wordWeights.GetValueOrDefault(word))
            }).Select(item => new
            {
                item.Recipe,
                item.AuthorScore,
                item.CategoryScore,
                item.IngredientScore,
                item.NameScore,
                Score = item.AuthorScore + item.CategoryScore + item.IngredientScore + item.NameScore
            }).ToArray();

        var relevant = new Queue<Guid>(scored.Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score).Select(item => item.Recipe.Id));
        var random = new Random(BitConverter.ToInt32(SHA256.HashData(userId.ToByteArray()
            .Concat(BitConverter.GetBytes(DateOnly.FromDateTime(DateTime.UtcNow).DayNumber)).ToArray())));
        var unrelated = new Queue<Guid>(scored.Where(item => item.Score == 0)
            .OrderBy(_ => random.Next()).Select(item => item.Recipe.Id));
        var ranked = new List<(Guid Id, string Reason)>(scored.Length);

        while (relevant.Count > 0 || unrelated.Count > 0)
        {
            var useUnrelated = unrelated.Count > 0 && (relevant.Count == 0 || random.NextDouble() < 0.05);
            ranked.Add(useUnrelated
                ? (unrelated.Dequeue(), relevant.Count == 0 ? "no relevant candidates remaining" : "exploration")
                : (relevant.Dequeue(), "relevance"));
        }

        var pageIds = ranked.Take(batchSize).ToArray();
        var recipes = pageIds.Length == 0
            ? []
            : await recipeRepository.GetByIdsWithAuthorAsync(pageIds.Select(item => item.Id).ToArray(), userId, cancellationToken);
        var byId = recipes.ToDictionary(recipe => recipe.Id);

        if (recipeLoggingOptions.Value.PersonalizedRecipesLogging && logger.IsEnabled(LogLevel.Information))
        {
            var loggedPageIds = pageIds.Skip((int)Math.Min((long)dto.PageIndex * dto.PageSize, pageIds.Length))
                .Take(dto.PageSize).ToArray();
            var scoresById = scored.ToDictionary(item => item.Recipe.Id);
            logger.LogInformation("====================");
            logger.LogInformation(
                "Personalized recipes for user {UserId}: {InteractionCount} interactions, {CandidateCount} candidates, {EligibleCount} eligible, page {PageIndex} size {PageSize}, {ReturnedCount} returned",
                userId, interactions.Count, candidates.Count, ranked.Count, dto.PageIndex, dto.PageSize,
                loggedPageIds.Count(item => byId.ContainsKey(item.Id)));

            var returned = loggedPageIds.Where(item => byId.ContainsKey(item.Id)).ToArray();
            var matchedIngredientIds = returned.SelectMany(item => scoresById[item.Id].Recipe.IngredientIds)
                .Where(id => ingredientWeights.GetValueOrDefault(id) > 0).Distinct().ToArray();
            var ingredientsById = matchedIngredientIds.Length == 0
                ? new Dictionary<Guid, Ingredient>()
                : (await ingredientRepository.GetByIdsWithTranslationsAsync(matchedIngredientIds, cancellationToken))
                    .ToDictionary(ingredient => ingredient.Id);
            var categoryIds = returned.Select(item => scoresById[item.Id].Recipe.CategoryId)
                .Where(id => categoryWeights.GetValueOrDefault(id) > 0).ToHashSet();
            var categoriesById = categoryIds.Count == 0
                ? new Dictionary<Guid, Category>()
                : (await categoryRepository.GetAllAsync(cancellationToken)).Where(category => categoryIds.Contains(category.Id))
                    .ToDictionary(category => category.Id);

            foreach (var (index, item) in loggedPageIds.Index())
            {
                if (!byId.ContainsKey(item.Id))
                {
                    continue;
                }

                var score = scoresById[item.Id];
                var authorSources = FormatSources(interactions.Where(interaction => interaction.Recipe.AuthorId == score.Recipe.AuthorId));
                var categorySources = FormatSources(interactions.Where(interaction => interaction.Recipe.CategoryId == score.Recipe.CategoryId));
                var categoryName = categoriesById.TryGetValue(score.Recipe.CategoryId, out var category)
                    ? category.Translations.FirstOrDefault(translation => translation.LanguageCode == "en")?.Name ?? score.Recipe.CategoryId.ToString()
                    : score.Recipe.CategoryId.ToString();
                var ingredientSources = score.Recipe.IngredientIds.Distinct()
                    .Where(id => ingredientWeights.GetValueOrDefault(id) > 0)
                    .Select(id =>
                    {
                        var name = ingredientsById.TryGetValue(id, out var ingredient)
                            ? ingredient.Translations.FirstOrDefault(translation => translation.LanguageCode == "en")?.Name ?? id.ToString()
                            : id.ToString();
                        var sources = FormatSources(interactions.Where(interaction => interaction.Recipe.IngredientIds.Contains(id)));
                        return $"{name}: {sources}";
                    });
                var nameSources = NameWords(score.Recipe.Name)
                    .Where(word => wordWeights.GetValueOrDefault(word) > 0)
                    .OrderBy(word => word, StringComparer.OrdinalIgnoreCase)
                    .Select(word => $"{word}: {FormatSources(interactions.Where(interaction => NameWords(interaction.Recipe.Name).Contains(word)))}");

                logger.LogInformation(
                    "Personalized recipe for user {UserId}: recipe {RecipeId} ({RecipeName}), rank {Rank}, reason {Reason}\n" +
                        "author {AuthorMultiplier} * {AuthorWeight} ({AuthorSources}) = {AuthorScore}\n" +
                        "category {CategoryMultiplier} * {CategoryWeight} ({CategoryName}: {CategorySources}) = {CategoryScore}\n" +
                        "ingredients {IngredientMultiplier} * {IngredientWeight} ({IngredientSources}) = {IngredientScore}\n" +
                        "name {NameMultiplier} * {NameWeight} ({NameSources}) = {NameScore}\n" +
                        "total {TotalScore}",
                    userId, item.Id, score.Recipe.Name, (long)dto.PageIndex * dto.PageSize + index + 1, item.Reason,
                    authorMultiplier, authorWeights.GetValueOrDefault(score.Recipe.AuthorId), authorSources, score.AuthorScore,
                    categoryMultiplier, categoryWeights.GetValueOrDefault(score.Recipe.CategoryId), categoryName, categorySources, score.CategoryScore,
                    ingredientMultiplier, score.IngredientScore / ingredientMultiplier, FormatMatches(ingredientSources), score.IngredientScore,
                    nameMultiplier, score.NameScore / nameMultiplier, FormatMatches(nameSources), score.NameScore, score.Score);
            }

            logger.LogInformation("====================");
        }

        return new PageResponse<FeedRecipeDto>
        {
            Data = pageIds.Where(item => byId.ContainsKey(item.Id)).Select(item => ToFeedRecipeDto(byId[item.Id], userId)).ToArray(),
            TotalSize = ranked.Count,
            PageIndex = 0,
            PageSize = dto.PageSize
        };
    }

    private static HashSet<string> NameWords(string name) => Regex.Matches(name, @"[\p{L}\p{M}\p{N}]+")
        .Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string FormatSources(IEnumerable<RecipeInteractionData> interactions) =>
        FormatMatches(interactions.Select(interaction =>
            $"{interaction.Recipe.Name} (+{InteractionWeight(interaction)})"), ", ");

    private static int InteractionWeight(RecipeInteractionData interaction) =>
        (interaction.IsLiked ? LikedInteractionWeight : 0)
        + (interaction.IsSaved ? SavedInteractionWeight : 0)
        + (interaction.IsShared ? ShareInteractionWeight : 0)
        + (interaction.IsLiked || interaction.IsSaved || interaction.IsShared ? 0 : 1);

    private static string FormatMatches(IEnumerable<string> matches, string separator = "; ")
    {
        var formatted = string.Join(separator, matches);
        return formatted.Length == 0 ? "-" : formatted;
    }

    public async Task<PageResponse<RecipeDto>> GetRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        var publicQuery = new GetRecipesDto
        {
            OwnerId = dto.OwnerId, Search = dto.Search, CategoryIds = dto.CategoryIds,
            IngredientIds = dto.IngredientIds, PageIndex = dto.PageIndex, PageSize = dto.PageSize
        };
        var cached = await readCache.GetOrCreateAsync($"recipes:list:{JsonSerializer.Serialize(publicQuery)}", token => GetRecipesCoreAsync(publicQuery, token), cancellationToken);
        var result = ClonePage(cached);
        await interactionsBuffer.ApplyAsync(result.Data, null, cancellationToken);
        return result;
    }

    public async Task<PageResponse<RecipeDto>> GetDraftRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        var ownQuery = new GetRecipesDto
        {
            ApplicationUserId = dto.ApplicationUserId,
            OwnerId = dto.ApplicationUserId,
            Search = dto.Search,
            IngredientIds = dto.IngredientIds,
            CategoryIds = dto.CategoryIds,
            PageIndex = dto.PageIndex,
            PageSize = dto.PageSize
        };
        var page = await recipeRepository.GetDraftRecipesAsync(ownQuery, cancellationToken);
        return new PageResponse<RecipeDto>
        {
            Data = page.Data.Select(ToRecipeDto).ToArray(),
            TotalSize = page.TotalSize,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize
        };
    }

    public async Task<PageResponse<FeedRecipeDto>> GetFollowingRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        var userId = dto.ApplicationUserId!.Value;
        var required = (dto.PageIndex + 1L) * dto.PageSize;
        var take = (int)Math.Min(int.MaxValue, required + dto.PageSize);
        var entries = await followingFeedCache.GetUserFeedAsync(userId, take, cancellationToken);
        if (entries.Count < required)
        {
            var seedTake = Math.Max(take, cacheOptions.Value.FollowingFeedMaxEntries);
            entries = await recipeRepository.GetFollowingFeedEntriesAsync(userId, seedTake, cancellationToken);
            await followingFeedCache.ReplaceUserFeedAsync(userId, entries, cancellationToken);
        }

        var followingIds = await followerRepository.GetFollowingIdsAsync(userId, cancellationToken);
        var newerThan = entries.Count == 0 ? DateTime.UnixEpoch : entries.Max(item => item.CreatedAt);
        var celebrityEntries = await followingFeedCache.GetCelebrityRecipesAsync(followingIds, newerThan,
            cacheOptions.Value.CelebrityFeedTakePerAuthor, cancellationToken);
        var merged = entries.Concat(celebrityEntries)
            .GroupBy(item => item.RecipeId)
            .Select(group => group.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.RecipeId).First())
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.RecipeId)
            .ToArray();
        var offset = (long)dto.PageIndex * dto.PageSize;
        if (offset > int.MaxValue)
        {
            return new PageResponse<FeedRecipeDto>
            {
                Data = [],
                TotalSize = merged.Length,
                PageIndex = dto.PageIndex,
                PageSize = dto.PageSize
            };
        }

        if (offset >= merged.Length)
        {
            var fallback = await recipeRepository.GetFollowingRecipesAsync(dto, cancellationToken);
            var fallbackData = fallback.Data.Select(recipe => ToFeedRecipeDto(recipe, userId)).ToArray();
            await interactionsBuffer.ApplyAsync(fallbackData, userId, cancellationToken);
            return new PageResponse<FeedRecipeDto>
            {
                Data = fallbackData,
                TotalSize = fallback.TotalSize,
                PageIndex = fallback.PageIndex,
                PageSize = fallback.PageSize
            };
        }

        var pageIds = merged.Skip((int)offset).Take(dto.PageSize).Select(item => item.RecipeId).ToArray();
        var recipes = pageIds.Length == 0 ? [] : await recipeRepository.GetByIdsWithAuthorAsync(pageIds, userId, cancellationToken);
        var byId = recipes.ToDictionary(recipe => recipe.Id);
        var data = pageIds.Where(byId.ContainsKey).Select(id => ToFeedRecipeDto(byId[id], userId)).ToArray();
        await interactionsBuffer.ApplyAsync(data, userId, cancellationToken);
        return new PageResponse<FeedRecipeDto>
        {
            Data = data,
            TotalSize = merged.Length,
            PageIndex = dto.PageIndex,
            PageSize = dto.PageSize
        };
    }

    private async Task<PageResponse<RecipeDto>> GetRecipesCoreAsync(GetRecipesDto dto, CancellationToken cancellationToken)
    {
        var page = await recipeRepository.GetRecipesAsync(dto, cancellationToken);
        return new PageResponse<RecipeDto>
        {
            Data = page.Data.Select(recipe => ToRecipeDto(recipe)).ToList(),
            TotalSize = page.TotalSize,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize
        };
    }

    public async Task<PageResponse<FeedRecipeDto>> GetSavedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        if (interactionsBuffer.Enabled)
        {
            var userId = dto.ApplicationUserId!.Value;
            var pending = (await interactionsBuffer.GetPendingSavedAsync(userId, cancellationToken))
                .ToDictionary(item => item.RecipeId);
            if (pending.Count > 0)
            {
                var ids = await recipeRepository.GetSavedRecipeIdsAsync(dto, pending.Keys.ToArray(), cancellationToken);
                var ordered = ids.Select(item =>
                    pending.TryGetValue(item.Id, out var state)
                        ? (item.Id, SavedAt: state.IsSaved ? state.SavedAt : null)
                        : item)
                    .Where(item => item.SavedAt.HasValue)
                    .OrderByDescending(item => item.SavedAt)
                    .ThenBy(item => item.Id)
                    .ToArray();
                var offset = (long)dto.PageIndex * dto.PageSize;
                var pageIds = offset >= ordered.Length ? [] : ordered.Skip((int)offset).Take(dto.PageSize).Select(item => item.Id).ToArray();
                var recipes = pageIds.Length == 0 ? [] : await recipeRepository.GetByIdsWithAuthorAsync(pageIds, userId, cancellationToken);
                var byId = recipes.ToDictionary(recipe => recipe.Id);
                var data = pageIds.Where(byId.ContainsKey).Select(id => ToFeedRecipeDto(byId[id], userId)).ToArray();
                await interactionsBuffer.ApplyAsync(data, userId, cancellationToken);
                return new PageResponse<FeedRecipeDto>
                {
                    Data = data,
                    TotalSize = ordered.Length,
                    PageIndex = dto.PageIndex,
                    PageSize = dto.PageSize
                };
            }
        }

        var result = CloneFeedPage(await readCache.GetOrCreateAsync($"recipes:saved:{JsonSerializer.Serialize(dto)}", token => GetSavedRecipesCoreAsync(dto, token), cancellationToken));
        await interactionsBuffer.ApplyAsync(result.Data, dto.ApplicationUserId, cancellationToken);
        return result;
    }

    private async Task<PageResponse<FeedRecipeDto>> GetSavedRecipesCoreAsync(GetRecipesDto dto, CancellationToken cancellationToken)
    {
        var page = await recipeRepository.GetSavedRecipesAsync(dto, cancellationToken);
        return new PageResponse<FeedRecipeDto>
        {
            Data = page.Data.Select(recipe => ToFeedRecipeDto(recipe, dto.ApplicationUserId)).ToList(),
            TotalSize = page.TotalSize,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize
        };
    }

    private static PageResponse<RecipeDto> ClonePage(PageResponse<RecipeDto> page) =>
        JsonSerializer.Deserialize<PageResponse<RecipeDto>>(JsonSerializer.Serialize(page))!;

    private static PageResponse<FeedRecipeDto> CloneFeedPage(PageResponse<FeedRecipeDto> page) =>
        JsonSerializer.Deserialize<PageResponse<FeedRecipeDto>>(JsonSerializer.Serialize(page))!;

    public async IAsyncEnumerable<RecipeGenerationEvent> GenerateRecipeAsync(GenerateRecipeRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > 4000)
        {
            yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "Provide a message (up to 4000 characters)", Key = ErrorKey.BadRequest }]);
            yield break;
        }

        await foreach (var item in GenerateRecipeCoreAsync(request.LanguageCode, RecipeSystemPrompt.Value,
            catalog => [new AiPartDto(Text: $"User message: {message}\nCurrent recipe: {JsonSerializer.Serialize(request.Recipe, AiJsonOptions)}\nCatalog: {catalog}")],
            cancellationToken)) yield return item;
    }

    public async Task<Result<GeneratedRecipeImage>> GenerateRecipeImageAsync(GeneratedRecipeDto? recipe, Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!await HasActiveSubscriptionAsync(userId, cancellationToken))
        {
            return SubscriptionRequired<GeneratedRecipeImage>();
        }

        if (recipe == null || string.IsNullOrWhiteSpace(recipe.Name))
        {
            return new Result<GeneratedRecipeImage>
            {
                Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Recipe name is required" }]
            };
        }

        var recipeJson = JsonSerializer.Serialize(recipe, AiJsonOptions);
        if (recipeJson.Length > 8000)
        {
            return new Result<GeneratedRecipeImage>
            {
                Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Recipe is too long" }]
            };
        }

        var prompt = $"{ImageSystemPrompt.Value}\nRecipe: {recipeJson}";
        var image = await aiService.GenerateImageAsync(prompt, cancellationToken);
        return new Result<GeneratedRecipeImage> { Value = image with { IsAIGeneratedImage = true } };
    }

    public async Task<Result<IAsyncEnumerable<RecipeGenerationEvent>>> GenerateRecipeFromVideoAsync(
        GenerateRecipeFromVideoRequestDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await HasActiveSubscriptionAsync(userId, cancellationToken))
        {
            return SubscriptionRequired<IAsyncEnumerable<RecipeGenerationEvent>>();
        }

        return new Result<IAsyncEnumerable<RecipeGenerationEvent>>
        {
            Value = GenerateRecipeFromVideoCoreAsync(request, cancellationToken)
        };
    }

    private async IAsyncEnumerable<RecipeGenerationEvent> GenerateRecipeFromVideoCoreAsync(GenerateRecipeFromVideoRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var video = request.Video;
        if (video == null || request.FormFieldCount != 0 || request.FileCount != 1)
        {
            yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "Provide only one video", Key = ErrorKey.BadRequest }]);
            yield break;
        }

        if (!mediaOptions.Value.VideoExtensions.Contains(Path.GetExtension(video.FileName), StringComparer.OrdinalIgnoreCase)
            || !fileService.IsValidMedia(video.FileName, video.Length))
        {
            yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "Provide a supported video", Key = ErrorKey.BadRequest }]);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(request.LanguageCode))
        {
            yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "Language code is required", Key = ErrorKey.BadRequest }]);
            yield break;
        }

        var mimeType = Path.GetExtension(video.FileName).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            _ => throw new ArgumentException("Unsupported video type", nameof(video))
        };

        string? fileUri = null;
        try
        {
            fileUri = await videoStorage.UploadAsync(video, mimeType, cancellationToken);
            await foreach (var item in GenerateRecipeCoreAsync(request.LanguageCode, VideoSystemPrompt.Value,
                catalog => [new AiPartDto(FileUri: fileUri, MimeType: mimeType), new AiPartDto(Text: $"Catalog: {catalog}")],
                cancellationToken)) yield return item;
        }
        finally
        {
            if (fileUri != null)
            {
                try
                {
                    await videoStorage.DeleteAsync(fileUri, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    operationLogger.LogWarning(exception, "Could not delete temporary AI video {FileUri}", fileUri);
                }
            }
        }
    }

    private async IAsyncEnumerable<RecipeGenerationEvent> GenerateRecipeCoreAsync(string languageCode, string systemPrompt,
        Func<string, IReadOnlyList<AiPartDto>> createParts, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "Language code is required", Key = ErrorKey.BadRequest }]);
            yield break;
        }

        var ingredients = await ingredientRepository.GetAllWithTranslationsAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var catalogInput = new
        {
            languageCode,
            categories = categories.Select(category => new
            {
                category.Id,
                name = category.Translations.FirstOrDefault(translation => translation.LanguageCode == languageCode)?.Name
                    ?? category.Translations.FirstOrDefault(translation => translation.LanguageCode == "en")?.Name
            }),
            ingredients = ingredients.Select(ingredient => new
            {
                ingredient.Id,
                ingredient.ParentId,
                ingredient.Unit,
                name = ingredient.Translations.FirstOrDefault(translation => translation.LanguageCode == languageCode)?.Name
                    ?? ingredient.Translations.FirstOrDefault(translation => translation.LanguageCode == "en")?.Name
            })
        };
        var catalogJson = JsonSerializer.Serialize(catalogInput, AiJsonOptions);
        var response = new StringBuilder();
        await foreach (var chunk in aiService.GenerateStreamAsync(systemPrompt, [new AiMessageDto("user", createParts(catalogJson))], cancellationToken)
            .WithCancellation(cancellationToken))
        {
            response.Append(chunk);
            yield return new RecipeGenerationEvent("chunk", Text: chunk);
        }

        var generated = JsonSerializer.Deserialize<GenerateRecipeResponseDto>(response.ToString(), AiJsonOptions)
            ?? throw new JsonException("AI returned an empty recipe.");

        var recipe = generated?.Recipe;
        var catalog = ingredients.ToDictionary(ingredient => ingredient.Id);

        if (recipe == null || string.IsNullOrWhiteSpace(generated!.Message) || string.IsNullOrWhiteSpace(recipe.Name) || string.IsNullOrWhiteSpace(recipe.Description)
            || recipe.CookingTimeMinutes <= 0 || !categories.Any(category => category.Id == recipe.CategoryId)
            || recipe.Steps == null || recipe.Steps.Count == 0
            || recipe.Steps.Any(step => step == null || string.IsNullOrWhiteSpace(step.Name) || string.IsNullOrWhiteSpace(step.Description))
            || recipe.Ingredients == null || recipe.Ingredients.Count == 0)
        {
            yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "AI returned an incomplete recipe", Key = ErrorKey.UnexpectedError }]);
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.Name) || ingredient.Quantity <= 0
                || !Enum.IsDefined(ingredient.Unit))
            {
                yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "AI returned an invalid ingredient", Key = ErrorKey.UnexpectedError }]);
                yield break;
            }

            var existing = ingredient.Id.HasValue && catalog.TryGetValue(ingredient.Id.Value, out var byId)
                ? byId
                : ingredients.FirstOrDefault(item => item.Translations.Any(translation =>
                    (translation.LanguageCode == languageCode || translation.LanguageCode == "en")
                    && string.Equals(translation.Name, ingredient.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (existing != null)
            {
                ingredient.Id = existing.Id;
                ingredient.Name = existing.Translations.FirstOrDefault(translation => translation.LanguageCode == languageCode)?.Name ?? ingredient.Name;
                ingredient.ParentId = existing.ParentId;
                ingredient.Unit = existing.Unit;
            }
            else
            {
                ingredient.Id = null;
                ingredient.Name = ingredient.Name.Trim();
                if (ingredient.ParentId.HasValue && (!catalog.TryGetValue(ingredient.ParentId.Value, out var parent) || parent.ParentId.HasValue))
                {
                    yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "AI returned an invalid parent ingredient", Key = ErrorKey.UnexpectedError }]);
                    yield break;
                }

            }

            if (!seen.Add(ingredient.Id?.ToString() ?? ingredient.Name))
            {
                yield return new RecipeGenerationEvent("error", Errors: [new Error { Message = "AI returned duplicate ingredients", Key = ErrorKey.UnexpectedError }]);
                yield break;
            }
        }

        yield return new RecipeGenerationEvent("completed", Result: new GenerateRecipeResponseDto { Recipe = recipe, Message = generated.Message });
    }

    private async Task<bool> HasActiveSubscriptionAsync(Guid userId, CancellationToken cancellationToken) =>
        (await subscriptionService.GetStatusAsync(userId, cancellationToken)).RemainingSeconds > 0;

    private static Result<T> SubscriptionRequired<T>() => new()
    {
        Errors = [new Error { Key = ErrorKey.Forbidden, Message = "An active subscription is required" }]
    };

    private static string LoadRecipeSystemPrompt(string fileName)
    {
        using var stream = typeof(RecipeService).Assembly.GetManifestResourceStream($"Sizzle.Application.Prompts.{fileName}")
            ?? throw new InvalidOperationException("Recipe system prompt was not found.");
        using var reader = new StreamReader(stream);
        var prompt = reader.ReadToEnd();
        return !string.IsNullOrWhiteSpace(prompt) ? prompt : throw new InvalidOperationException("Recipe system prompt is empty.");
    }

    public async Task<CreateRecipeResult> CreateRecipeAsync(CreateRecipeDto dto, CancellationToken cancellationToken = default)
    {
        var detailsError = await ValidateRecipeDetailsAsync(dto.CategoryId, dto.CookingTimeMinutes, cancellationToken);
        if (detailsError != null)
        {
            return new CreateRecipeResult { Errors = [detailsError] };
        }

        var mediaError = ValidateMedia(dto.File, dto.Steps);
        if (mediaError != null)
        {
            return new CreateRecipeResult { Errors = [mediaError] };
        }

        if (dto.IsDraft)
        {
            return await CreateRecipeCoreAsync(dto, [], cancellationToken);
        }

        var outcome = await ReviewRecipeAsync(dto, cancellationToken);
        if (outcome.Error != null)
        {
            return new CreateRecipeResult { Errors = [outcome.Error], CorrectedRecipe = outcome.CorrectedRecipe };
        }

        return await CreateRecipeCoreAsync(dto, outcome.ApprovedIngredients, cancellationToken);
    }

    private async Task<RecipeReviewOutcomeDto> ReviewRecipeAsync(CreateRecipeDto dto, CancellationToken cancellationToken)
    {
        var catalog = await ingredientRepository.GetAllWithTranslationsAsync(cancellationToken);
        var locales = catalog.SelectMany(ingredient => ingredient.Translations)
            .Select(translation => translation.LanguageCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var known = catalog.ToDictionary(ingredient => ingredient.Id);
        if (dto.Ingredients.Any(ingredient => ingredient == null || !known.ContainsKey(ingredient.Id)))
        {
            return new RecipeReviewOutcomeDto([], new Error { Key = ErrorKey.NotFound, Message = "Ingredient not found" });
        }

        RecipeReviewDto review;
        try
        {
            review = await ReviewCreationAsync(dto, catalog, locales, cancellationToken);
        }
        catch (JsonException exception)
        {
            operationLogger.LogWarning(exception, "Invalid AI review for user {UserId}", dto.ApplicationUserId);
            return new RecipeReviewOutcomeDto([], new Error { Key = ErrorKey.UnexpectedError, Message = "Invalid recipe review" });
        }
        if (review.Status == RecipeReviewStatus.Rejected)
        {
            return new RecipeReviewOutcomeDto([], new Error { Key = ErrorKey.RecipeRejected, Message = review.Reason });
        }

        if (review.Status == RecipeReviewStatus.Correction && review.CorrectedRecipe != null &&
            IsValidCorrection(review.CorrectedRecipe, dto, known))
        {
            return new RecipeReviewOutcomeDto([], new Error { Key = ErrorKey.RecipeNeedsCorrection, Message = review.Reason },
                review.CorrectedRecipe);
        }

        if (review.Status != RecipeReviewStatus.Approved || !ValidTranslations(review.NewIngredients, dto.NewIngredients, locales))
        {
            return new RecipeReviewOutcomeDto([], new Error { Key = ErrorKey.UnexpectedError, Message = review.Reason });
        }

        var approvedIngredients = dto.NewIngredients.Select((item, index) =>
            new ApprovedNewIngredientDto(review.NewIngredients.Single(entry => entry.Index == index).Name.Trim(),
                item.ParentId, item.Unit, item.Quantity, review.NewIngredients.Single(entry => entry.Index == index).Calories!.Value,
                locales.ToDictionary(locale => locale, locale => review.NewIngredients.Single(entry => entry.Index == index)
                    .Translations.Single(translation => string.Equals(translation.Key, locale, StringComparison.OrdinalIgnoreCase)).Value)))
            .ToArray();
        return new RecipeReviewOutcomeDto(approvedIngredients);
    }

    private async Task<CreateRecipeResult> CreateRecipeCoreAsync(CreateRecipeDto dto,
        IReadOnlyCollection<ApprovedNewIngredientDto> approvedIngredients,
        CancellationToken cancellationToken)
    {
        var ingredients = await ResolveIngredientsAsync(dto.Ingredients, approvedIngredients, cancellationToken);
        if (!ingredients.IsSuccess)
        {
            return new CreateRecipeResult { Errors = ingredients.Errors };
        }

        var author = await recipeRepository.GetAuthorForMutationAsync(dto.ApplicationUserId, cancellationToken);
        var recipe = mapper.Map<Recipe>(dto);
        recipe.Id = Guid.NewGuid();
        recipe.IsDraft = dto.IsDraft;
        recipe.IsAIGeneratedImage = dto.IsAIGeneratedImage;
        recipe.ApplicationUser = author;
        recipe.CreationDate = DateTime.UtcNow;
        recipe.RecipeSteps = dto.Steps.Select((step, index) =>
        {
            var recipeStep = mapper.Map<RecipeStep>(step);
            recipeStep.Id = Guid.NewGuid();
            recipeStep.Position = index;
            return recipeStep;
        }).ToList();
        recipe.RecipeIngredients = ingredients.Value!.Quantities.Select(entry =>
            new RecipeIngredient { IngredientId = entry.Key, Ingredient = ingredients.Value.ById[entry.Key], Quantity = entry.Value }).ToList();

        var savedPaths = new List<string>();
        try
        {
            if (dto.File != null)
            {
                recipe.FilePath = await SaveMediaAsync(dto.File, recipe.Id, savedPaths);
            }

            await SaveStepMediaAsync(recipe.RecipeSteps, dto.Steps, recipe.Id, savedPaths);
            recipeRepository.Add(recipe);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            operationLogger.LogError(exception, "Recipe creation failed for user {UserId}", dto.ApplicationUserId);
            foreach (var path in savedPaths)
            {
                fileService.DeleteFile(path);
            }

            throw;
        }

        operationLogger.LogInformation("Recipe {RecipeId} created by user {UserId}", recipe.Id, dto.ApplicationUserId);
        var resultDto = ToFeedRecipeDto(recipe, dto.ApplicationUserId);
        await ingredientCache.IngredientsCreatedAsync(ingredients.Value!.Created);
        await readCache.UpdateListsAsync(new RecipeListMutation(null, ToListSnapshot(recipe, recipe.RecipeSteps), resultDto,
            author?.Followers.Select(follower => follower.FollowerId).ToArray()), cancellationToken: cancellationToken);
        if (!recipe.IsDraft)
        {
            await eventQueue.EnqueueAsync(new RecipeCreatedEventDto(recipe.Id, recipe.ApplicationUserId, recipe.CreationDate), cancellationToken);
            await notificationSender.RecipeCreatedAsync(recipe.Id, recipe.ApplicationUserId, recipe.Name, recipe.CreationDate);
        }
        return new CreateRecipeResult
        {
            Value = ToRecipeDto(recipe)
        };
    }

    private async Task<RecipeReviewDto> ReviewCreationAsync(CreateRecipeDto dto, List<Ingredient> catalog, IReadOnlyCollection<string> locales,
        CancellationToken cancellationToken)
    {
        var parts = new List<AiPartDto>
        {
            new(Text: JsonSerializer.Serialize(new
            {
                recipe = new { dto.Name, dto.Description, dto.CookingTimeMinutes, dto.CategoryId, dto.IsDraft,
                    steps = dto.Steps.Select(step => new { step.Name, step.Description }), dto.Ingredients, dto.NewIngredients },
                catalog = catalog.Select(ingredient => new { ingredient.Id, ingredient.ParentId, ingredient.Unit, ingredient.Calories,
                    translations = ingredient.Translations.Select(translation => new { translation.LanguageCode, translation.Name }) }),
                locales
            }, AiJsonOptions))
        };
        var uploadedVideos = new List<string>();
        try
        {
            if (dto.File != null)
            {
                await AddReviewMediaAsync(parts, dto.File, "Recipe cover", uploadedVideos, cancellationToken);
            }

            foreach (var (step, index) in dto.Steps.Select((step, index) => (step, index)))
            {
                foreach (var upload in step.Files)
                {
                    await AddReviewMediaAsync(parts, upload, $"Step {index + 1}: {step.Name}", uploadedVideos, cancellationToken);
                }
            }

            var json = await aiService.GenerateJsonAsync(CreationReviewPrompt.Value,
                [new AiMessageDto("user", parts)], cancellationToken);
            return JsonSerializer.Deserialize<RecipeReviewDto>(json, AiJsonOptions)
                ?? throw new InvalidOperationException("AI returned an empty recipe review.");
        }
        finally
        {
            foreach (var uri in uploadedVideos)
            {
                await videoStorage.DeleteAsync(uri, CancellationToken.None);
            }
        }
    }

    private async Task AddReviewMediaAsync(List<AiPartDto> parts, RecipeMediaUploadDto upload, string label,
        List<string> uploadedVideos, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        var mimeType = extension switch
        {
            ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif",
            ".mp4" => "video/mp4", ".mov" => "video/quicktime", ".webm" => "video/webm",
            ".avi" => "video/x-msvideo", ".mkv" => "video/x-matroska",
            _ => "image/jpeg"
        };
        parts.Add(new AiPartDto(Text: $"{label}: {upload.FileName}"));
        if (mediaOptions.Value.VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            var uri = await videoStorage.UploadAsync(upload, mimeType, cancellationToken);
            uploadedVideos.Add(uri);
            parts.Add(new AiPartDto(FileUri: uri, MimeType: mimeType));
        }
        else
        {
            await using var stream = upload.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            parts.Add(new AiPartDto(Data: buffer.ToArray(), MimeType: mimeType));
        }
    }

    private static bool ValidTranslations(List<ReviewedIngredientTranslationDto>? entries, NewIngredientDto[] inputs,
        IReadOnlyCollection<string> locales)
    {
        if (entries == null || entries.Count != inputs.Length || entries.Select(entry => entry.Index).Distinct().Count() != inputs.Length)
        {
            return false;
        }

        if (inputs.Length > 0 && locales.Count == 0)
        {
            return false;
        }

        var expectedLocales = locales.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var canonicalLocale = locales.FirstOrDefault();
        return entries.All(entry => entry.Index >= 0 && entry.Index < inputs.Length &&
            !string.IsNullOrWhiteSpace(entry.Name) &&
            entry.Calories is >= 0 &&
            entry.Translations != null && entry.Translations.Count == expectedLocales.Count &&
            entry.Translations.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expectedLocales) &&
            entry.Translations.All(translation => !string.IsNullOrWhiteSpace(translation.Value)) &&
            entry.Translations.Any(translation =>
                string.Equals(translation.Key, canonicalLocale, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(translation.Value.Trim(), entry.Name.Trim(), StringComparison.OrdinalIgnoreCase))) &&
            entries.Select(entry => entry.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == entries.Count;
    }

    private static bool IsValidCorrection(GeneratedRecipeDto corrected, CreateRecipeDto input,
        Dictionary<Guid, Ingredient> catalog)
    {
        if (corrected.CategoryId != input.CategoryId || corrected.CookingTimeMinutes <= 0 ||
            string.IsNullOrWhiteSpace(corrected.Name) || corrected.Steps == null ||
            corrected.Steps.Count != input.Steps.Length || corrected.Ingredients == null ||
            corrected.Ingredients.Count != input.Ingredients.Length + input.NewIngredients.Length ||
            corrected.Ingredients.Any(ingredient => ingredient.Quantity <= 0 ||
                (ingredient.Id.HasValue && (!catalog.TryGetValue(ingredient.Id.Value, out var existing) ||
                    existing.Unit != ingredient.Unit || existing.ParentId != ingredient.ParentId ||
                    !existing.Translations.Any(translation => string.Equals(translation.Name, ingredient.Name,
                        StringComparison.OrdinalIgnoreCase)))) ||
                (!ingredient.Id.HasValue && (string.IsNullOrWhiteSpace(ingredient.Name) ||
                    (ingredient.ParentId.HasValue && (!catalog.TryGetValue(ingredient.ParentId.Value, out var parent) || parent.ParentId.HasValue))))))
        {
            return false;
        }

        return input.Ingredients.All(selected => corrected.Ingredients.Any(ingredient =>
            ingredient.Id == selected.Id && ingredient.Quantity == selected.Quantity));
    }

    public async Task<Result<RecipeDetailsDto>> GetRecipeByIdAsync(GetRecipeByIdDto dto, CancellationToken cancellationToken = default)
    {
        var cached = await readCache.GetOrCreateAsync($"recipes:detail:{dto.Id:N}:{dto.ApplicationUserId}:{dto.LanguageCode}",
            token => GetRecipeByIdCoreAsync(dto, token), cancellationToken);
        var result = JsonSerializer.Deserialize<Result<RecipeDetailsDto>>(JsonSerializer.Serialize(cached))!;
        if (result.Value != null) await interactionsBuffer.ApplyAsync([result.Value], dto.ApplicationUserId, cancellationToken);
        return result;
    }

    private async Task<Result<RecipeDetailsDto>> GetRecipeByIdCoreAsync(GetRecipeByIdDto dto, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetWithFullDetailsAsync(dto.Id, dto.ApplicationUserId, cancellationToken);
        if (recipe == null)
        {
            return new Result<RecipeDetailsDto>
            {
                Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }]
            };
        }

        return new Result<RecipeDetailsDto>
        {
            Value = mapper.Map<RecipeDetailsDto>(recipe, options =>
            {
                options.Items["ViewerId"] = dto.ApplicationUserId;
                options.Items["LanguageCode"] = dto.LanguageCode;
            })
        };
    }

    public async Task<UpdateRecipeResult> UpdateRecipeAsync(UpdateRecipeDto dto, CancellationToken cancellationToken = default)
    {
        var recipe = await recipeRepository.GetWithDetailsAsync(dto.Id, dto.ApplicationUserId, cancellationToken);
        if (recipe == null)
        {
            return new UpdateRecipeResult { Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }] };
        }

        if (recipe.ApplicationUserId != dto.ApplicationUserId)
        {
            return new UpdateRecipeResult { Errors = [new Error { Message = "Recipe does not belong to current user", Key = ErrorKey.BadRequest }] };
        }

        var detailsError = await ValidateRecipeDetailsAsync(dto.CategoryId, dto.CookingTimeMinutes, cancellationToken);
        if (detailsError != null)
        {
            return new UpdateRecipeResult { Errors = [detailsError] };
        }

        var mediaError = ValidateMedia(dto.File, dto.Steps);
        if (mediaError != null)
        {
            return new UpdateRecipeResult { Errors = [mediaError] };
        }

        var isDraft = dto.IsDraft ?? recipe.IsDraft;
        IReadOnlyCollection<ApprovedNewIngredientDto> approvedIngredients = [];
        if (!isDraft)
        {
            var reviewInput = new CreateRecipeDto
            {
                ApplicationUserId = dto.ApplicationUserId, Name = dto.Name, Description = dto.Description,
                CookingTimeMinutes = dto.CookingTimeMinutes, CategoryId = dto.CategoryId, File = dto.File,
                Steps = dto.Steps, Ingredients = dto.Ingredients, NewIngredients = dto.NewIngredients
            };
            var outcome = await ReviewRecipeAsync(reviewInput, cancellationToken);
            if (outcome.Error != null)
            {
                return new UpdateRecipeResult { Errors = [outcome.Error], CorrectedRecipe = outcome.CorrectedRecipe };
            }

            approvedIngredients = outcome.ApprovedIngredients;
        }

        var ingredients = await ResolveIngredientsAsync(dto.Ingredients, approvedIngredients, cancellationToken);
        if (!ingredients.IsSuccess)
        {
            return new UpdateRecipeResult { Errors = ingredients.Errors };
        }

        var before = ToListSnapshot(recipe, recipe.RecipeSteps);
        mapper.Map(dto, recipe);
        recipe.IsDraft = isDraft;

        var oldPaths = recipe.RecipeSteps.SelectMany(step => step.Files).Select(file => file.FilePath).ToList();
        recipeRepository.RemoveSteps(recipe.RecipeSteps.ToList());
        recipe.RecipeSteps.Clear();
        var newSteps = dto.Steps.Select((step, index) =>
        {
            var recipeStep = mapper.Map<RecipeStep>(step);
            recipeStep.Id = Guid.NewGuid();
            recipeStep.RecipeId = recipe.Id;
            recipeStep.Position = index;
            return recipeStep;
        }).ToList();

        var selectedIds = ingredients.Value!.Quantities.Keys.ToHashSet();
        var removedIngredients = recipe.RecipeIngredients.Where(link => !selectedIds.Contains(link.IngredientId)).ToList();
        recipeRepository.RemoveIngredients(removedIngredients);
        foreach (var link in removedIngredients)
        {
            recipe.RecipeIngredients.Remove(link);
        }

        var currentLinks = recipe.RecipeIngredients.ToDictionary(link => link.IngredientId);
        foreach (var (ingredientId, quantity) in ingredients.Value.Quantities)
        {
            if (currentLinks.TryGetValue(ingredientId, out var link))
            {
                link.Quantity = quantity;
                link.Ingredient = ingredients.Value.ById[ingredientId];
            }
            else
            {
                recipe.RecipeIngredients.Add(new RecipeIngredient
                {
                    IngredientId = ingredientId, Ingredient = ingredients.Value.ById[ingredientId], Quantity = quantity
                });
            }
        }

        var savedPaths = new List<string>();
        try
        {
            if (dto.File != null)
            {
                if (recipe.FilePath != null)
                {
                    oldPaths.Add(recipe.FilePath);
                }

                recipe.FilePath = await SaveMediaAsync(dto.File, recipe.Id, savedPaths);
            }

            await SaveStepMediaAsync(newSteps, dto.Steps, recipe.Id, savedPaths);
            recipeRepository.AddSteps(newSteps);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            operationLogger.LogError(exception, "Recipe update failed for recipe {RecipeId} and user {UserId}", dto.Id, dto.ApplicationUserId);
            foreach (var path in savedPaths)
            {
                fileService.DeleteFile(path);
            }

            throw;
        }

        operationLogger.LogInformation("Recipe {RecipeId} updated by user {UserId}", recipe.Id, dto.ApplicationUserId);
        foreach (var path in oldPaths)
        {
            fileService.DeleteFile(path);
        }

        var resultDto = ToFeedRecipeDto(recipe, dto.ApplicationUserId);
        await ingredientCache.IngredientsCreatedAsync(ingredients.Value!.Created);
        await readCache.UpdateListsAsync(new RecipeListMutation(before, ToListSnapshot(recipe, newSteps), resultDto), language => new RecipeDetailsDto
        {
            Id = recipe.Id,
            UserId = recipe.ApplicationUserId,
            UserName = resultDto.UserName,
            UserImageUrl = resultDto.UserImageUrl,
            IsFollowed = resultDto.IsFollowed,
            Name = recipe.Name,
            Description = recipe.Description,
            IsDraft = recipe.IsDraft,
            IsAIGeneratedImage = recipe.IsAIGeneratedImage,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Calories = RecipeCalories.Calculate(recipe.RecipeIngredients),
            CategoryId = recipe.CategoryId,
            FileUrl = resultDto.FileUrl,
            ViewCount = recipe.ViewCount,
            LikeCount = recipe.LikeCount,
            CreationDate = recipe.CreationDate,
            IsLiked = resultDto.IsLiked,
            IsSaved = resultDto.IsSaved,
            IsSavedAt = resultDto.IsSavedAt,
            Steps = newSteps.OrderBy(step => step.Position).Select(step => new RecipeStepDto
            {
                Id = step.Id,
                Name = step.Name,
                Description = step.Description,
                FileUrls = step.Files.Select(file => fileService.GetFileUrl(file.FilePath))
                    .Where(url => url != null).Select(url => url!).ToArray()
            }).ToArray(),
            Ingredients = ingredients.Value!.Quantities.Select(entry =>
            {
                var ingredient = ingredients.Value.ById[entry.Key];
                return new RecipeIngredientDto
                {
                    Id = entry.Key,
                    Quantity = entry.Value,
                    Unit = unitFormatter.Format(ingredient.Unit, language),
                    Name = ingredient.Translations.FirstOrDefault(translation => translation.LanguageCode == language)?.Name
                        ?? ingredient.Translations.FirstOrDefault(translation => translation.LanguageCode == "en")?.Name
                        ?? string.Empty
                };
            }).ToArray()
        }, cancellationToken);
        if (!before.IsDraft && recipe.IsDraft)
        {
            var author = await recipeRepository.GetAuthorForMutationAsync(recipe.ApplicationUserId, cancellationToken);
            await followingFeedCache.RemoveRecipeAsync(recipe.ApplicationUserId, recipe.Id,
                author?.Followers.Select(follower => follower.FollowerId).ToArray() ?? [], cancellationToken);
        }
        if (before.IsDraft && !recipe.IsDraft)
        {
            await eventQueue.EnqueueAsync(new RecipeCreatedEventDto(recipe.Id, recipe.ApplicationUserId, recipe.CreationDate), cancellationToken);
            await notificationSender.RecipeCreatedAsync(recipe.Id, recipe.ApplicationUserId, recipe.Name, recipe.CreationDate);
        }
        return new UpdateRecipeResult
        {
            Value = ToRecipeDto(recipe)
        };
    }

    public async Task<Result> DeleteRecipeAsync(Guid recipeId, Guid applicationUserId, CancellationToken cancellationToken = default)
    {
        var recipe = await recipeRepository.GetWithDetailsAsync(recipeId, applicationUserId, cancellationToken);
        if (recipe == null)
        {
            operationLogger.LogWarning("Recipe deletion rejected for recipe {RecipeId} and user {UserId}: {ErrorKey}", recipeId, applicationUserId, ErrorKey.NotFound);
            return new Result
            {
                Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }]
            };
        }

        if (recipe.ApplicationUserId != applicationUserId)
        {
            operationLogger.LogWarning("Recipe deletion rejected for recipe {RecipeId} and user {UserId}: {ErrorKey}", recipeId, applicationUserId, ErrorKey.BadRequest);
            return new Result
            {
                Errors = [new Error { Message = "Recipe does not belong to current user", Key = ErrorKey.BadRequest }]
            };
        }

        var before = ToListSnapshot(recipe, recipe.RecipeSteps);
        var paths = recipe.RecipeSteps.SelectMany(step => step.Files).Select(file => file.FilePath).ToList();
        if (recipe.FilePath != null)
        {
            paths.Add(recipe.FilePath);
        }

        recipeRepository.Remove(recipe);
        await unitOfWork.CommitAsync(cancellationToken);
        operationLogger.LogInformation("Recipe {RecipeId} deleted by user {UserId}", recipeId, applicationUserId);

        foreach (var path in paths)
        {
            fileService.DeleteFile(path);
        }

        await readCache.UpdateListsAsync(new RecipeListMutation(before, null, null), cancellationToken: cancellationToken);
        await commentCache.InvalidateAsync(recipeId);
        return new Result();
    }

    public async Task<Result<RecipeLikeDto>> ToggleLikeAsync(ToggleLikeRecipeDto dto, CancellationToken cancellationToken = default)
    {
        var isLiked = interactionsBuffer.Enabled
            ? await interactionsBuffer.ToggleLikeAsync(dto.ApplicationUserId, dto.RecipeId, cancellationToken)
            : await recipeRepository.ToggleLikeAsync(dto.ApplicationUserId, dto.RecipeId, cancellationToken);
        if (isLiked == null)
        {
            return Reject<RecipeLikeDto>("toggle like", dto.RecipeId, dto.ApplicationUserId,
                new Error { Message = "Recipe not found", Key = ErrorKey.NotFound });
        }

        operationLogger.LogInformation("Recipe {RecipeId} like state changed to {IsLiked} by user {UserId}", dto.RecipeId, isLiked.Value, dto.ApplicationUserId);
        return new Result<RecipeLikeDto>
        {
            Value = new RecipeLikeDto
            {
                RecipeId = dto.RecipeId,
                IsLiked = isLiked.Value
            }
        };
    }

    public async Task<Result<RecipeSaveDto>> ToggleSaveAsync(Guid recipeId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (interactionsBuffer.Enabled)
        {
            var saved = await interactionsBuffer.ToggleSaveAsync(userId, recipeId, cancellationToken);
            if (saved == null)
            {
                return Reject<RecipeSaveDto>("toggle save", recipeId, userId,
                    new Error { Message = "Recipe not found", Key = ErrorKey.NotFound });
            }
            operationLogger.LogInformation("Recipe {RecipeId} save state changed to {IsSaved} by user {UserId}", recipeId, saved.IsSaved, userId);
            return new Result<RecipeSaveDto> { Value = saved };
        }

        if (await recipeRepository.GetAsync(cancellationToken, recipeId) is not { IsDraft: false })
        {
            return Reject<RecipeSaveDto>("toggle save", recipeId, userId,
                new Error { Message = "Recipe not found", Key = ErrorKey.NotFound });
        }

        var link = await recipeRepository.GetUserRecipeAsync(userId, recipeId, cancellationToken);
        if (link == null)
        {
            link = new ApplicationUserRecipe { ApplicationUserId = userId, RecipeId = recipeId };
            recipeRepository.AddUserRecipe(link);
        }

        link.IsSaved = !link.IsSaved;
        link.IsSavedAt = link.IsSaved ? DateTime.UtcNow : null;
        await unitOfWork.CommitAsync(cancellationToken);
        operationLogger.LogInformation("Recipe {RecipeId} save state changed to {IsSaved} by user {UserId}", recipeId, link.IsSaved, userId);

        return new Result<RecipeSaveDto> { Value = new RecipeSaveDto(recipeId, link.IsSaved, link.IsSavedAt) };
    }

    public async Task<Result<RecipeShareDto>> UpdateShareAsync(Guid recipeId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (interactionsBuffer.Enabled)
        {
            var shared = await interactionsBuffer.UpdateShareAsync(userId, recipeId, cancellationToken);
            if (shared == null)
            {
                return Reject<RecipeShareDto>("share", recipeId, userId,
                    new Error { Message = "Recipe not found", Key = ErrorKey.NotFound });
            }
            operationLogger.LogInformation("Recipe {RecipeId} shared by user {UserId}", recipeId, userId);
            return new Result<RecipeShareDto> { Value = shared };
        }

        if (await recipeRepository.GetAsync(cancellationToken, recipeId) is not { IsDraft: false })
        {
            return Reject<RecipeShareDto>("share", recipeId, userId,
                new Error { Message = "Recipe not found", Key = ErrorKey.NotFound });
        }

        var link = await recipeRepository.GetUserRecipeAsync(userId, recipeId, cancellationToken);
        if (link == null)
        {
            link = new ApplicationUserRecipe { ApplicationUserId = userId, RecipeId = recipeId };
            recipeRepository.AddUserRecipe(link);
        }

        link.IsShared = true;
        link.IsSharedAt = DateTime.UtcNow;
        await unitOfWork.CommitAsync(cancellationToken);
        operationLogger.LogInformation("Recipe {RecipeId} shared by user {UserId}", recipeId, userId);

        return new Result<RecipeShareDto> { Value = new RecipeShareDto(recipeId, link.IsShared, link.IsSharedAt.Value) };
    }

    public async Task<Result> UpdateViewAsync(UpdateRecipeViewDto dto, CancellationToken cancellationToken = default)
    {
        if (interactionsBuffer.Enabled)
        {
            if (!await interactionsBuffer.UpdateViewAsync(dto.ApplicationUserId, dto.RecipeId,
                    businessLogicOptions.Value.RecipeViewUpdateIntervalMinutes, cancellationToken))
            {
                return ViewNotFound(dto);
            }

            return new Result();
        }

        var recipe = await recipeRepository.GetAsync(cancellationToken, dto.RecipeId);
        if (recipe == null || recipe.IsDraft)
        {
            return ViewNotFound(dto);
        }

        var userRecipe = await recipeRepository.GetUserRecipeAsync(dto.ApplicationUserId, dto.RecipeId, cancellationToken);
        var now = DateTime.UtcNow;

        if (userRecipe == null)
        {
            userRecipe = new ApplicationUserRecipe
            {
                ApplicationUserId = dto.ApplicationUserId,
                RecipeId = dto.RecipeId,
                IsLiked = false,
                ViewUpdatedAt = now
            };
            recipeRepository.AddUserRecipe(userRecipe);
            recipe.ViewCount += 1;
            await unitOfWork.CommitAsync(cancellationToken);
        }
        else
        {
            var canUpdateView = !userRecipe.ViewUpdatedAt.HasValue
                || now >= userRecipe.ViewUpdatedAt.Value.AddMinutes(businessLogicOptions.Value.RecipeViewUpdateIntervalMinutes);

            if (canUpdateView)
            {
                recipe.ViewCount += 1;
                userRecipe.ViewUpdatedAt = now;
                await unitOfWork.CommitAsync(cancellationToken);
            }
        }

        return new Result();
    }

    private Result ViewNotFound(UpdateRecipeViewDto dto)
    {
        operationLogger.LogWarning("Recipe update view rejected for recipe {RecipeId} and user {UserId}: {ErrorKey}",
            dto.RecipeId, dto.ApplicationUserId, ErrorKey.NotFound);
        return new Result { Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }] };
    }

    private RecipeDto ToRecipeDto(Recipe recipe) => mapper.Map<RecipeDto>(recipe);

    private FeedRecipeDto ToFeedRecipeDto(Recipe recipe, Guid? applicationUserId) =>
        mapper.Map<FeedRecipeDto>(recipe, options => options.Items["ViewerId"] = applicationUserId);

    private Result<T> Reject<T>(string operation, Guid? recipeId, Guid userId, params Error[] errors)
    {
        operationLogger.LogWarning("Recipe {Operation} rejected for recipe {RecipeId} and user {UserId}: {ErrorKeys}",
            operation, recipeId, userId, string.Join(", ", errors.Select(error => error.Key)));
        return new Result<T> { Errors = errors.ToList() };
    }

    private static RecipeListSnapshot ToListSnapshot(Recipe recipe, IEnumerable<RecipeStep> steps) => new(
        recipe.Id, recipe.ApplicationUserId, recipe.CategoryId, recipe.Name, recipe.Description,
        steps.SelectMany(step => new[] { step.Name, step.Description }).ToArray(),
        recipe.RecipeIngredients.Select(link => link.IngredientId).ToArray(), recipe.LikeCount, recipe.CreationDate, recipe.IsDraft);

    private async Task<Error?> ValidateRecipeDetailsAsync(Guid categoryId, int cookingTimeMinutes, CancellationToken cancellationToken)
    {
        if (cookingTimeMinutes <= 0)
        {
            return new Error { Message = "Cooking time must be greater than zero", Key = ErrorKey.BadRequest };
        }

        if (categoryId == Guid.Empty || !await categoryRepository.ExistsAsync(categoryId, cancellationToken))
        {
            return new Error { Message = "Category not found", Key = ErrorKey.NotFound };
        }

        return null;
    }

    private Error? ValidateMedia(RecipeMediaUploadDto? file, RecipeStepInputDto[] steps)
    {
        var files = steps.SelectMany(step => step.Files);
        if (file != null)
        {
            files = files.Append(file);
        }

        return files.Any(upload => !fileService.IsValidMedia(upload.FileName, upload.Length))
            ? new Error { Message = "Unsupported media type or file size", Key = ErrorKey.BadRequest }
            : null;
    }

    private async Task<string> SaveMediaAsync(RecipeMediaUploadDto upload, Guid recipeId, List<string> savedPaths)
    {
        var path = Path.Combine("recipes", recipeId.ToString("N"), $"{Guid.NewGuid():N}{Path.GetExtension(upload.FileName).ToLowerInvariant()}");
        savedPaths.Add(path);
        await using var stream = upload.OpenReadStream();
        await fileService.SaveFile(stream, path);
        return path;
    }

    private async Task SaveStepMediaAsync(IEnumerable<RecipeStep> steps, RecipeStepInputDto[] inputs, Guid recipeId, List<string> savedPaths)
    {
        foreach (var (step, input) in steps.Zip(inputs))
        {
            foreach (var upload in input.Files)
            {
                step.Files.Add(new RecipeStepFile
                {
                    Id = Guid.NewGuid(),
                    FilePath = await SaveMediaAsync(upload, recipeId, savedPaths)
                });
            }
        }
    }

    private async Task<Result<ResolvedRecipeIngredients>> ResolveIngredientsAsync(RecipeIngredientInputDto[] selectedIngredients,
        IReadOnlyCollection<ApprovedNewIngredientDto> newIngredients, CancellationToken cancellationToken)
    {
        if (selectedIngredients.Any(ingredient => ingredient == null || ingredient.Id == Guid.Empty))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Invalid ingredient id", Key = ErrorKey.BadRequest }] };
        }

        if (selectedIngredients.Any(ingredient => ingredient.Quantity <= 0) || newIngredients.Any(ingredient => ingredient == null || ingredient.Quantity <= 0))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Ingredient quantity must be greater than zero", Key = ErrorKey.BadRequest }] };
        }

        if (selectedIngredients.Select(ingredient => ingredient.Id).Distinct().Count() != selectedIngredients.Length)
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Duplicate ingredient id", Key = ErrorKey.BadRequest }] };
        }

        if (newIngredients.Any(ingredient => ingredient == null || string.IsNullOrWhiteSpace(ingredient.Name)))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Ingredient name cannot be empty", Key = ErrorKey.BadRequest }] };
        }

        if (newIngredients.Any(ingredient => !Enum.IsDefined(ingredient.Unit)))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Invalid ingredient unit", Key = ErrorKey.BadRequest }] };
        }

        if (newIngredients.Any(ingredient => ingredient.Calories < 0 || ingredient.Translations == null || ingredient.Translations.Any(translation =>
            string.IsNullOrWhiteSpace(translation.Key) || string.IsNullOrWhiteSpace(translation.Value))
            || !ingredient.Translations.Values.Any(name =>
                string.Equals(name.Trim(), ingredient.Name.Trim(), StringComparison.OrdinalIgnoreCase))))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Invalid ingredient translations", Key = ErrorKey.BadRequest }] };
        }

        var quantities = selectedIngredients.ToDictionary(ingredient => ingredient.Id, ingredient => ingredient.Quantity);
        var ids = quantities.Keys.ToHashSet();
        var parentIds = newIngredients.Where(ingredient => ingredient.ParentId.HasValue)
            .Select(ingredient => ingredient.ParentId!.Value).Distinct().ToHashSet();
        if (parentIds.Contains(Guid.Empty))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Invalid parent ingredient id", Key = ErrorKey.BadRequest }] };
        }

        var existing = await ingredientRepository.GetByIdsWithTranslationsTrackedAsync(ids.Concat(parentIds).Distinct().ToArray(), cancellationToken);
        var existingIds = existing.Select(ingredient => ingredient.Id).ToHashSet();
        if (!ids.IsSubsetOf(existingIds))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Ingredient not found", Key = ErrorKey.NotFound }] };
        }

        if (!parentIds.IsSubsetOf(existingIds))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Parent ingredient not found", Key = ErrorKey.NotFound }] };
        }

        if (existing.Any(ingredient => parentIds.Contains(ingredient.Id) && ingredient.ParentId.HasValue))
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Parent ingredient cannot have a parent", Key = ErrorKey.BadRequest }] };
        }

        var ingredientsToResolve = newIngredients.Select(ingredient => new ApprovedNewIngredientDto(ingredient.Name.Trim(),
            ingredient.ParentId, ingredient.Unit, ingredient.Quantity, ingredient.Calories, ingredient.Translations)).ToArray();
        if (ingredientsToResolve.Length == 0)
        {
            return new Result<ResolvedRecipeIngredients> { Value = new ResolvedRecipeIngredients(quantities, existing.ToDictionary(item => item.Id), []) };
        }

        if (ingredientsToResolve.Select(ingredient => ingredient.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ingredientsToResolve.Length)
        {
            return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Duplicate ingredient name", Key = ErrorKey.BadRequest }] };
        }

        var matched = await ingredientRepository.GetByNamesAsync(ingredientsToResolve
            .Select(ingredient => ingredient.Name.ToLowerInvariant()).Distinct().ToArray(), cancellationToken);
        var created = new List<Ingredient>();
        foreach (var input in ingredientsToResolve)
        {
            var ingredient = matched.FirstOrDefault(i => i.Translations.Any(t =>
                string.Equals(t.Name, input.Name, StringComparison.OrdinalIgnoreCase)));

            if (ingredient == null)
            {
                ingredient = new Ingredient
                {
                    Id = Guid.NewGuid(),
                    ParentId = input.ParentId,
                    Unit = input.Unit,
                    Calories = input.Calories,
                    Translations = input.Translations.Select(translation => new IngredientTranslation
                    {
                        LanguageCode = translation.Key, Name = translation.Value.Trim()
                    }).ToList()
                };
                ingredientRepository.Add(ingredient);
                matched.Add(ingredient);
                created.Add(ingredient);
            }

            if (!quantities.TryAdd(ingredient.Id, input.Quantity))
            {
                return new Result<ResolvedRecipeIngredients> { Errors = [new Error { Message = "Duplicate ingredient", Key = ErrorKey.BadRequest }] };
            }
        }

        return new Result<ResolvedRecipeIngredients> { Value = new ResolvedRecipeIngredients(quantities,
            existing.Concat(matched).DistinctBy(item => item.Id).ToDictionary(item => item.Id), created) };
    }
}
