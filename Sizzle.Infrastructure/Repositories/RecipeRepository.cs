using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class RecipeRepository(ApplicationDbContext dbContext, ILogger<RecipeRepository>? logger = null) : Repository<Recipe>(dbContext), IRecipeRepository
{
    private readonly ILogger<RecipeRepository> logger = logger ?? NullLogger<RecipeRepository>.Instance;

    public Task<ApplicationUser?> GetAuthorForMutationAsync(Guid authorId, CancellationToken cancellationToken = default) =>
        DbGetAuthorForMutationAsync(authorId, cancellationToken);

    private Task<ApplicationUser?> DbGetAuthorForMutationAsync(Guid authorId, CancellationToken cancellationToken)
    {
        logger.LogInformation("db.recipes.get_author_for_mutation");
        return DbContext.Users.Include(user => user.Followers).FirstOrDefaultAsync(user => user.Id == authorId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeInteractionData>> GetRecentInteractionsAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_recent_interactions");
        return await DbContext.ApplicationUserRecipes.AsNoTracking()
            .Where(link => link.ApplicationUserId == userId && !link.Recipe!.IsDraft)
            .OrderByDescending(link => link.IsSharedAt ?? link.ViewUpdatedAt ?? link.IsSavedAt ?? link.Recipe!.CreationDate)
            .ThenBy(link => link.RecipeId)
            .Take(count)
            .Select(link => new RecipeInteractionData(
                new RecipeRecommendationData(
                    link.RecipeId,
                    link.Recipe!.ApplicationUserId,
                    link.Recipe.CategoryId,
                    link.Recipe.Name,
                    link.Recipe.RecipeIngredients.Select(ingredient => ingredient.IngredientId).ToArray()),
                link.IsLiked,
                link.IsSaved,
                link.IsShared))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeRecommendationData>> GetRecommendationCandidatesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_recommendation_candidates");
        var query = DbSet.AsNoTracking().Where(recipe => !recipe.IsDraft && recipe.ApplicationUserId != dto.ApplicationUserId);

        if (dto.CategoryIds.Length > 0)
        {
            query = query.Where(recipe => dto.CategoryIds.Contains(recipe.CategoryId));
        }

        foreach (var ingredientId in dto.IngredientIds.Distinct())
        {
            var id = ingredientId;
            query = query.Where(recipe => recipe.RecipeIngredients.Any(ingredient => ingredient.IngredientId == id));
        }

        return await query
            .OrderByDescending(recipe => recipe.CreationDate)
            .ThenBy(recipe => recipe.Id)
            .Select(recipe => new RecipeRecommendationData(
                recipe.Id,
                recipe.ApplicationUserId,
                recipe.CategoryId,
                recipe.Name,
                recipe.RecipeIngredients.Select(ingredient => ingredient.IngredientId).ToArray()))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetByIdsWithAuthorAsync(IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_by_ids_with_author");
        return await DbSet.AsNoTracking()
            .Where(recipe => !recipe.IsDraft && ids.Contains(recipe.Id))
            .Include(recipe => recipe.ApplicationUser)
            .Include(recipe => recipe.RecipeIngredients).ThenInclude(link => link.Ingredient)
            .Include(recipe => recipe.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && (link.IsLiked || link.IsSaved)))
            .Include(recipe => recipe.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<PageResponse<Recipe>> GetRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_page");
        return await GetRecipePageAsync(ApplyFilters(DbSet.AsNoTracking().Where(recipe => !recipe.IsDraft), dto), dto, cancellationToken);
    }

    public async Task<PageResponse<Recipe>> GetDraftRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_draft_page");
        if (dto.OwnerId == null) throw new ArgumentException("Draft queries require an owner", nameof(dto));
        return await GetRecipePageAsync(ApplyFilters(DbSet.AsNoTracking().Where(recipe => recipe.IsDraft), dto), dto, cancellationToken);
    }

    private static async Task<PageResponse<Recipe>> GetRecipePageAsync(IQueryable<Recipe> query, GetRecipesDto dto, CancellationToken cancellationToken)
    {
        var totalSize = await query.CountAsync(cancellationToken);
        var offset = (long)dto.PageIndex * dto.PageSize;
        if (offset > int.MaxValue)
        {
            return new PageResponse<Recipe>
            {
                TotalSize = totalSize,
                PageIndex = dto.PageIndex,
                PageSize = dto.PageSize
            };
        }

        IQueryable<Recipe> page = query
            .OrderByDescending(r => r.LikeCount)
            .ThenByDescending(r => r.CreationDate)
            .ThenBy(r => r.Id)
            .Skip((int)offset)
            .Take(dto.PageSize)
            .Include(r => r.ApplicationUser)
            .Include(r => r.RecipeIngredients).ThenInclude(link => link.Ingredient)
            .AsSplitQuery();

        return new PageResponse<Recipe>
        {
            Data = await page.ToListAsync(cancellationToken),
            TotalSize = totalSize,
            PageIndex = dto.PageIndex,
            PageSize = dto.PageSize
        };
    }

    public async Task<PageResponse<Recipe>> GetFollowingRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_following_page");
        var userId = dto.ApplicationUserId!.Value;
        var query = DbSet.AsNoTracking()
            .Where(recipe => !recipe.IsDraft && recipe.ApplicationUser.Followers.Any(follow => follow.FollowerId == userId));
        var totalSize = await query.CountAsync(cancellationToken);
        var offset = (long)dto.PageIndex * dto.PageSize;
        if (offset > int.MaxValue)
        {
            return new PageResponse<Recipe>
            {
                TotalSize = totalSize,
                PageIndex = dto.PageIndex,
                PageSize = dto.PageSize
            };
        }

        var page = await query
            .OrderByDescending(recipe => recipe.CreationDate)
            .ThenBy(recipe => recipe.Id)
            .Skip((int)offset)
            .Take(dto.PageSize)
            .Include(recipe => recipe.ApplicationUser)
            .Include(recipe => recipe.RecipeIngredients).ThenInclude(link => link.Ingredient)
            .Include(recipe => recipe.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && (link.IsSaved || link.IsLiked)))
            .Include(recipe => recipe.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PageResponse<Recipe>
        {
            Data = page,
            TotalSize = totalSize,
            PageIndex = dto.PageIndex,
            PageSize = dto.PageSize
        };
    }

    public async Task<IReadOnlyList<FollowingFeedCacheEntry>> GetFollowingFeedEntriesAsync(Guid followerId, int take, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_following_feed_entries");
        return await DbSet.AsNoTracking()
            .Where(recipe => !recipe.IsDraft && recipe.ApplicationUser.Followers.Any(follow => follow.FollowerId == followerId))
            .OrderByDescending(recipe => recipe.CreationDate)
            .ThenBy(recipe => recipe.Id)
            .Take(take)
            .Select(recipe => new FollowingFeedCacheEntry(recipe.Id, recipe.ApplicationUserId, recipe.CreationDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<PageResponse<Recipe>> GetSavedRecipesAsync(GetRecipesDto dto, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_saved_page");
        var userId = dto.ApplicationUserId!.Value;
        var query = ApplyFilters(DbSet.AsNoTracking()
            .Where(recipe => !recipe.IsDraft && recipe.ApplicationUserRecipes.Any(link => link.ApplicationUserId == userId && link.IsSaved)), dto);
        var totalSize = await query.CountAsync(cancellationToken);
        var offset = (long)dto.PageIndex * dto.PageSize;
        if (offset > int.MaxValue)
        {
            return new PageResponse<Recipe>
            {
                TotalSize = totalSize,
                PageIndex = dto.PageIndex,
                PageSize = dto.PageSize
            };
        }

        var page = await query
            .OrderByDescending(recipe => recipe.ApplicationUserRecipes
                .Where(link => link.ApplicationUserId == userId && link.IsSaved)
                .Max(link => link.IsSavedAt))
            .ThenBy(recipe => recipe.Id)
            .Skip((int)offset)
            .Take(dto.PageSize)
            .Include(recipe => recipe.ApplicationUser)
            .Include(recipe => recipe.RecipeIngredients).ThenInclude(link => link.Ingredient)
            .Include(recipe => recipe.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && (link.IsSaved || link.IsLiked)))
            .Include(recipe => recipe.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PageResponse<Recipe>
        {
            Data = page,
            TotalSize = totalSize,
            PageIndex = dto.PageIndex,
            PageSize = dto.PageSize
        };
    }

    public async Task<IReadOnlyList<(Guid Id, DateTime? SavedAt)>> GetSavedRecipeIdsAsync(GetRecipesDto dto, Guid[] pendingIds, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_saved_ids");
        var userId = dto.ApplicationUserId!.Value;
        var query = ApplyFilters(DbSet.AsNoTracking().Where(recipe => !recipe.IsDraft && (
            recipe.ApplicationUserRecipes.Any(link => link.ApplicationUserId == userId && link.IsSaved)
            || pendingIds.Contains(recipe.Id))), dto);
        var entries = await query.Select(recipe => new
        {
            recipe.Id,
            SavedAt = recipe.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && link.IsSaved)
                .Max(link => link.IsSavedAt)
        }).ToListAsync(cancellationToken);
        return entries.Select(entry => (entry.Id, entry.SavedAt)).ToList();
    }

    private static IQueryable<Recipe> ApplyFilters(IQueryable<Recipe> query, GetRecipesDto dto)
    {
        if (dto.OwnerId is Guid ownerId)
        {
            query = query.Where(r => r.ApplicationUserId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(dto.Search))
        {
            var search = dto.Search.Trim();
            query = query.Where(r => r.Name.Contains(search)
                || r.Description.Contains(search)
                || r.RecipeSteps.Any(step => step.Name.Contains(search) || step.Description.Contains(search)));
        }

        foreach (var ingredientId in dto.IngredientIds.Distinct())
        {
            var id = ingredientId;
            query = query.Where(r => r.RecipeIngredients.Any(ingredient => ingredient.IngredientId == id));
        }

        if (dto.CategoryIds.Length > 0)
        {
            query = query.Where(r => dto.CategoryIds.Contains(r.CategoryId));
        }

        return query;
    }

    public async Task<Recipe?> GetOwnedByUserAsync(Guid recipeId, Guid applicationUserId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_owned_by_user");
        return await DbSet.FirstOrDefaultAsync(r => r.Id == recipeId && r.ApplicationUserId == applicationUserId, cancellationToken);
    }

    public Task<Recipe?> GetWithDetailsAsync(Guid recipeId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_with_details");
        var query = DbSet.Include(r => r.RecipeSteps).ThenInclude(step => step.Files)
            .Include(r => r.RecipeIngredients).ThenInclude(link => link.Ingredient)
            .Include(r => r.ApplicationUser).ThenInclude(user => user!.Followers.Where(follower => follower.FollowerId == userId))
            .AsSplitQuery();
        if (userId.HasValue)
            query = query.Include(r => r.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId));
        return query.FirstOrDefaultAsync(r => r.Id == recipeId && (!r.IsDraft || r.ApplicationUserId == userId), cancellationToken);
    }

    public Task<Recipe?> GetWithAuthorAsync(Guid recipeId, Guid? applicationUserId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_with_author");
        var query = DbSet.AsNoTracking().Include(r => r.ApplicationUser)
            .Include(r => r.RecipeIngredients).ThenInclude(link => link.Ingredient).AsSplitQuery();
        if (applicationUserId is Guid userId)
        {
            query = query.Include(r => r.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && (link.IsLiked || link.IsSaved)))
                .Include(r => r.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId));
        }

        return query.FirstOrDefaultAsync(r => r.Id == recipeId && (!r.IsDraft || r.ApplicationUserId == applicationUserId), cancellationToken);
    }

    public Task<Recipe?> GetWithFullDetailsAsync(Guid recipeId, Guid? applicationUserId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_with_full_details");
        var query = DbSet.AsNoTracking()
            .Include(r => r.ApplicationUser)
            .Include(r => r.RecipeSteps).ThenInclude(step => step.Files)
            .Include(r => r.RecipeIngredients).ThenInclude(link => link.Ingredient)
                .ThenInclude(ingredient => ingredient.Translations)
            .AsSplitQuery();

        if (applicationUserId is Guid userId)
        {
            query = query.Include(r => r.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && (link.IsLiked || link.IsSaved)))
                .Include(r => r.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId));
        }

        return query.FirstOrDefaultAsync(r => r.Id == recipeId && (!r.IsDraft || r.ApplicationUserId == applicationUserId), cancellationToken);
    }

    public void AddSteps(IEnumerable<RecipeStep> steps)
    {
        logger.LogInformation("db.recipes.add_steps");
        DbContext.RecipeSteps.AddRange(steps);
    }

    public void RemoveSteps(IEnumerable<RecipeStep> steps)
    {
        logger.LogInformation("db.recipes.remove_steps");
        DbContext.RecipeSteps.RemoveRange(steps);
    }

    public void RemoveIngredients(IEnumerable<RecipeIngredient> ingredients)
    {
        logger.LogInformation("db.recipes.remove_ingredients");
        DbContext.RecipeIngredients.RemoveRange(ingredients);
    }

    public async Task<ApplicationUserRecipe?> GetUserRecipeAsync(Guid applicationUserId, Guid recipeId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.get_user_recipe");
        return await DbContext.ApplicationUserRecipes
            .FirstOrDefaultAsync(x => x.ApplicationUserId == applicationUserId && x.RecipeId == recipeId, cancellationToken);
    }

    public async Task<bool?> ToggleLikeAsync(Guid applicationUserId, Guid recipeId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.recipes.toggle_like");
        await using var transaction = DbContext.Database.IsRelational()
            ? await DbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        var recipe = await DbSet.FirstOrDefaultAsync(item => item.Id == recipeId && !item.IsDraft, cancellationToken);
        if (recipe == null)
        {
            return null;
        }

        var link = await DbContext.ApplicationUserRecipes
            .FirstOrDefaultAsync(item => item.ApplicationUserId == applicationUserId && item.RecipeId == recipeId, cancellationToken);
        var isLiked = link == null || !link.IsLiked;
        if (link == null)
        {
            DbContext.ApplicationUserRecipes.Add(new ApplicationUserRecipe
            {
                ApplicationUserId = applicationUserId,
                RecipeId = recipeId,
                IsLiked = true
            });
        }
        else
        {
            link.IsLiked = isLiked;
        }

        if (transaction == null)
        {
            recipe.LikeCount += isLiked ? 1 : -1;
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null)
        {
            await DbSet.Where(item => item.Id == recipeId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LikeCount,
                        item => item.LikeCount + (isLiked ? 1 : -1)), cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
        }

        return isLiked;
    }

    public void AddUserRecipe(ApplicationUserRecipe userRecipe)
    {
        logger.LogInformation("db.recipes.add_user_recipe");
        _ = DbContext.ApplicationUserRecipes.Add(userRecipe);
    }
}
