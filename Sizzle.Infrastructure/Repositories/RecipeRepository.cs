using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public class RecipeRepository(ApplicationDbContext dbContext) : Repository<Recipe>(dbContext), IRecipeRepository
{
    public async Task<PageResponse<Recipe>> GetRecipesAsync(GetRecipesDto dto)
    {
        var query = DbSet.AsNoTracking();

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

        var totalSize = await query.CountAsync();
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
            .OrderByDescending(r => r.CreationDate)
            .ThenBy(r => r.Id)
            .Skip((int)offset)
            .Take(dto.PageSize)
            .Include(r => r.ApplicationUser)
            .AsSplitQuery();

        if (dto.ApplicationUserId is Guid userId)
        {
            page = page.Include(r => r.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && link.IsLiked))
                .Include(r => r.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId));
        }

        return new PageResponse<Recipe>
        {
            Data = await page.ToListAsync(),
            TotalSize = totalSize,
            PageIndex = dto.PageIndex,
            PageSize = dto.PageSize
        };
    }

    public async Task<Recipe?> GetOwnedByUserAsync(Guid recipeId, Guid applicationUserId)
    {
        return await DbSet.FirstOrDefaultAsync(r => r.Id == recipeId && r.ApplicationUserId == applicationUserId);
    }

    public Task<Recipe?> GetWithDetailsAsync(Guid recipeId)
    {
        return DbSet.Include(r => r.RecipeSteps).ThenInclude(step => step.Files)
            .Include(r => r.RecipeIngredients)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == recipeId);
    }

    public Task<Recipe?> GetWithAuthorAsync(Guid recipeId, Guid? applicationUserId)
    {
        var query = DbSet.AsNoTracking().Include(r => r.ApplicationUser).AsSplitQuery();
        if (applicationUserId is Guid userId)
        {
            query = query.Include(r => r.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && link.IsLiked))
                .Include(r => r.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId));
        }

        return query.FirstOrDefaultAsync(r => r.Id == recipeId);
    }

    public Task<Recipe?> GetWithFullDetailsAsync(Guid recipeId, Guid? applicationUserId)
    {
        var query = DbSet.AsNoTracking()
            .Include(r => r.ApplicationUser)
            .Include(r => r.RecipeSteps).ThenInclude(step => step.Files)
            .Include(r => r.RecipeIngredients).ThenInclude(link => link.Ingredient)
                .ThenInclude(ingredient => ingredient.Translations)
            .AsSplitQuery();

        if (applicationUserId is Guid userId)
        {
            query = query.Include(r => r.ApplicationUserRecipes.Where(link => link.ApplicationUserId == userId && link.IsLiked))
                .Include(r => r.ApplicationUser!.Followers.Where(follow => follow.FollowerId == userId));
        }

        return query.FirstOrDefaultAsync(r => r.Id == recipeId);
    }

    public void AddSteps(IEnumerable<RecipeStep> steps)
    {
        DbContext.RecipeSteps.AddRange(steps);
    }

    public void RemoveSteps(IEnumerable<RecipeStep> steps)
    {
        DbContext.RecipeSteps.RemoveRange(steps);
    }

    public void RemoveIngredients(IEnumerable<RecipeIngredient> ingredients)
    {
        DbContext.RecipeIngredients.RemoveRange(ingredients);
    }

    public async Task<ApplicationUserRecipe?> GetUserRecipeAsync(Guid applicationUserId, Guid recipeId)
    {
        return await DbContext.ApplicationUserRecipes
            .FirstOrDefaultAsync(x => x.ApplicationUserId == applicationUserId && x.RecipeId == recipeId);
    }

    public void AddUserRecipe(ApplicationUserRecipe userRecipe)
    {
        _ = DbContext.ApplicationUserRecipes.Add(userRecipe);
    }
}
