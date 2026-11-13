using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Entities;
using StackExchange.Redis;

namespace Sizzle.Infrastructure.Cache;

public sealed class RecipeInteractionBuffer(ApplicationDbContext db, IConnectionMultiplexer connection, IOptions<RecipeCacheOptions> cacheOptions) : IRecipeInteractionBuffer
{
    private const string QueueKey = "sizzle:interactions:pending";
    private readonly IDatabase redis = connection.GetDatabase();

    public bool Enabled => true;

    public async Task<bool?> ToggleLikeAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default) =>
        await ChangeAsync(userId, recipeId, state =>
        {
            state.IsLiked = !state.IsLiked;
            return state.IsLiked;
        }, cancellationToken);

    public Task<RecipeSaveDto?> ToggleSaveAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default) =>
        ChangeAsync(userId, recipeId, state =>
        {
            state.IsSaved = !state.IsSaved;
            state.IsSavedAt = state.IsSaved ? DateTime.UtcNow : null;
            return new RecipeSaveDto(recipeId, state.IsSaved, state.IsSavedAt);
        }, cancellationToken);

    public Task<RecipeShareDto?> UpdateShareAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default) =>
        ChangeAsync(userId, recipeId, state =>
        {
            state.IsShared = true;
            state.IsSharedAt = DateTime.UtcNow;
            return new RecipeShareDto(recipeId, true, state.IsSharedAt.Value);
        }, cancellationToken);

    public async Task<bool> UpdateViewAsync(Guid userId, Guid recipeId, int cooldownMinutes, CancellationToken cancellationToken = default)
    {
        var result = await ChangeAsync(userId, recipeId, state =>
        {
            var now = DateTime.UtcNow;
            if (state.ViewUpdatedAt == null || now >= state.ViewUpdatedAt.Value.AddMinutes(cooldownMinutes))
            {
                state.ViewUpdatedAt = now;
                state.ViewDelta++;
            }
            return true;
        }, cancellationToken);
        return result == true;
    }

    private async Task<T?> ChangeAsync<T>(Guid userId, Guid recipeId, Func<PendingInteraction, T> change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = $"{recipeId:N}:{userId:N}";
        var key = StateKey(id);
        var token = Guid.NewGuid().ToString("N");
        for (var attempt = 0; !await redis.LockTakeAsync(LockKey(id), token, TimeSpan.FromMinutes(2)); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attempt == 100) throw new TimeoutException("Could not acquire the recipe interaction lock.");
            await Task.Delay(25, cancellationToken);
        }

        try
        {
            if (!await db.Recipes.AnyAsync(recipe => recipe.Id == recipeId && !recipe.IsDraft, cancellationToken)) return default;

            var existing = await redis.StringGetAsync(key);
            PendingInteraction state;
            if (existing.HasValue)
            {
                state = JsonSerializer.Deserialize<PendingInteraction>((string)existing!)!;
            }
            else
            {
                var link = await db.ApplicationUserRecipes.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.ApplicationUserId == userId && item.RecipeId == recipeId, cancellationToken);
                state = new PendingInteraction
                {
                    UserId = userId,
                    RecipeId = recipeId,
                    BaseIsLiked = link?.IsLiked == true,
                    IsLiked = link?.IsLiked == true,
                    IsSaved = link?.IsSaved == true,
                    IsSavedAt = link?.IsSavedAt,
                    IsShared = link?.IsShared == true,
                    IsSharedAt = link?.IsSharedAt,
                    ViewUpdatedAt = link?.ViewUpdatedAt
                };
            }

            var result = change(state);
            var transaction = redis.CreateTransaction();
            _ = transaction.StringSetAsync(key, JsonSerializer.Serialize(state));
            _ = transaction.SetAddAsync(QueueKey, id);
            _ = transaction.SetAddAsync(RecipeKey(recipeId), id);
            _ = transaction.SetAddAsync(UserKey(userId), id);
            _ = transaction.SetAddAsync(SeenKey(userId), recipeId.ToString("N"));
            _ = transaction.KeyExpireAsync(SeenKey(userId), TimeSpan.FromSeconds(cacheOptions.Value.DurationSeconds * 2L));
            cancellationToken.ThrowIfCancellationRequested();
            if (!await transaction.ExecuteAsync()) throw new InvalidOperationException("Unable to queue recipe interaction.");
            return result;
        }
        finally
        {
            await redis.LockReleaseAsync(LockKey(id), token);
        }
    }

    public async Task ApplyAsync(IReadOnlyCollection<RecipeDto> recipes, Guid? userId, CancellationToken cancellationToken = default)
    {
        foreach (var recipe in recipes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ids = await redis.SetMembersAsync(RecipeKey(recipe.Id));
            if (ids.Length == 0) continue;
            var values = await redis.StringGetAsync(ids.Select(id => (RedisKey)StateKey(id!)).ToArray());
            for (var i = 0; i < ids.Length; i++)
            {
                if (!values[i].HasValue) continue;
                var state = JsonSerializer.Deserialize<PendingInteraction>((string)values[i]!)!;
                recipe.LikeCount += (state.IsLiked ? 1 : 0) - (state.BaseIsLiked ? 1 : 0);
                recipe.ViewCount += state.ViewDelta;
                if (recipe is not FeedRecipeDto feed || state.UserId != userId) continue;
                feed.IsLiked = state.IsLiked;
                feed.IsSaved = state.IsSaved;
                feed.IsSavedAt = state.IsSavedAt;
            }
        }
    }

    public async Task<IReadOnlyList<(Guid RecipeId, bool IsSaved, DateTime? SavedAt)>> GetPendingSavedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = await redis.SetMembersAsync(UserKey(userId));
        if (ids.Length == 0) return [];
        var values = await redis.StringGetAsync(ids.Select(id => (RedisKey)StateKey(id!)).ToArray());
        return values.Where(value => value.HasValue)
            .Select(value => JsonSerializer.Deserialize<PendingInteraction>((string)value!)!)
            .Select(state => (state.RecipeId, state.IsSaved, state.IsSavedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<PendingRecipeInteraction>> GetPendingInteractionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = await redis.SetMembersAsync(UserKey(userId));
        var seen = await redis.SetMembersAsync(SeenKey(userId));
        var values = ids.Length == 0 ? [] : await redis.StringGetAsync(ids.Select(id => (RedisKey)StateKey(id!)).ToArray());
        var pending = values.Where(value => value.HasValue)
            .Select(value => JsonSerializer.Deserialize<PendingInteraction>((string)value!)!)
            .Select(state => new PendingRecipeInteraction(state.RecipeId, state.IsLiked, state.IsSaved,
                state.IsShared, state.IsSharedAt ?? state.ViewUpdatedAt ?? state.IsSavedAt))
            .ToList();
        var pendingIds = pending.Select(item => item.RecipeId).ToHashSet();
        pending.AddRange(seen.Select(value => Guid.ParseExact((string)value!, "N"))
            .Where(id => !pendingIds.Contains(id))
            .Select(id => new PendingRecipeInteraction(id, false, false, false, null)));
        return pending;
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        foreach (var member in await redis.SetMembersAsync(QueueKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = (string)member!;
            var token = Guid.NewGuid().ToString("N");
            if (!await redis.LockTakeAsync(LockKey(id), token, TimeSpan.FromMinutes(2))) continue;
            try
            {
                var value = await redis.StringGetAsync(StateKey(id));
                if (!value.HasValue)
                {
                    await redis.SetRemoveAsync(QueueKey, id);
                    continue;
                }

                var state = JsonSerializer.Deserialize<PendingInteraction>((string)value!)!;
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                    : null;
                var recipe = await db.Recipes.FirstOrDefaultAsync(item => item.Id == state.RecipeId, cancellationToken);
                var link = await db.ApplicationUserRecipes.FirstOrDefaultAsync(
                    item => item.ApplicationUserId == state.UserId && item.RecipeId == state.RecipeId, cancellationToken);
                if (recipe != null)
                {
                    if (link == null)
                    {
                        link = new ApplicationUserRecipe { ApplicationUserId = state.UserId, RecipeId = state.RecipeId };
                        db.ApplicationUserRecipes.Add(link);
                    }
                    recipe.LikeCount += (state.IsLiked ? 1 : 0) - (link.IsLiked ? 1 : 0);
                    if (state.ViewUpdatedAt > link.ViewUpdatedAt)
                    {
                        recipe.ViewCount += state.ViewDelta;
                        link.ViewUpdatedAt = state.ViewUpdatedAt;
                    }
                    link.IsLiked = state.IsLiked;
                    link.IsSaved = state.IsSaved;
                    link.IsSavedAt = state.IsSavedAt;
                    link.IsShared = state.IsShared;
                    link.IsSharedAt = state.IsSharedAt;
                    await db.SaveChangesAsync(cancellationToken);
                    if (transaction != null) await transaction.CommitAsync(cancellationToken);
                }

                var cleanup = redis.CreateTransaction();
                _ = cleanup.KeyDeleteAsync(StateKey(id));
                _ = cleanup.SetRemoveAsync(QueueKey, id);
                _ = cleanup.SetRemoveAsync(RecipeKey(state.RecipeId), id);
                _ = cleanup.SetRemoveAsync(UserKey(state.UserId), id);
                await cleanup.ExecuteAsync();
                db.ChangeTracker.Clear();
            }
            finally
            {
                await redis.LockReleaseAsync(LockKey(id), token);
            }
        }
    }

    private static string StateKey(string id) => $"sizzle:interaction:{id}";
    private static string LockKey(string id) => $"sizzle:interaction:lock:{id}";
    private static string RecipeKey(Guid recipeId) => $"sizzle:interaction:recipe:{recipeId:N}";
    private static string UserKey(Guid userId) => $"sizzle:interaction:user:{userId:N}";
    private static string SeenKey(Guid userId) => $"sizzle:interaction:seen:{userId:N}";

    private sealed class PendingInteraction
    {
        public PendingInteraction() { }
        public Guid UserId { get; set; }
        public Guid RecipeId { get; set; }
        public bool BaseIsLiked { get; set; }
        public bool IsLiked { get; set; }
        public bool IsSaved { get; set; }
        public DateTime? IsSavedAt { get; set; }
        public bool IsShared { get; set; }
        public DateTime? IsSharedAt { get; set; }
        public DateTime? ViewUpdatedAt { get; set; }
        public int ViewDelta { get; set; }
    }
}