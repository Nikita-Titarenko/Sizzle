using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;

namespace Sizzle.Infrastructure.Cache;

internal static class RecipeListPages
{
    public static bool Matches(RecipeListSnapshot? recipe, GetRecipesDto query)
    {
        if (recipe == null || recipe.IsDraft) return false;
        if (query.OwnerId.HasValue && query.OwnerId.Value != recipe.OwnerId) return false;
        if (query.CategoryIds.Length > 0 && !query.CategoryIds.Contains(recipe.CategoryId)) return false;
        if (query.Ingredients.Any(filter => !recipe.Ingredients.Any(ingredient =>
            ingredient.IngredientId == filter.IngredientId
            && (!filter.Quantity.HasValue || ingredient.Quantity <= filter.Quantity.Value)))) return false;
        if (string.IsNullOrWhiteSpace(query.Search)) return true;
        var search = query.Search.Trim();
        return recipe.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || recipe.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
            || recipe.StepTexts.Any(text => text.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<int, PageResponse<RecipeDto>> Update(
        IReadOnlyDictionary<int, PageResponse<RecipeDto>> pages, GetRecipesDto query,
        RecipeListMutation mutation, string origin)
    {
        var before = Matches(mutation.Before, query);
        var after = Matches(mutation.After, query);
        if (!before && !after) return pages.ToDictionary();

        var stable = before && after && mutation.Before!.LikeCount == mutation.After!.LikeCount
            && mutation.Before.CreationDate == mutation.After.CreationDate;
        if (stable)
        {
            return pages.ToDictionary(page => page.Key, page => new PageResponse<RecipeDto>
            {
                Data = page.Value.Data.Select(recipe => recipe.Id == mutation.After!.Id
                    ? PublicRecipe(mutation.Recipe!, origin) : recipe).ToArray(),
                PageIndex = page.Value.PageIndex,
                PageSize = page.Value.PageSize,
                TotalSize = page.Value.TotalSize
            });
        }

        if (!pages.ContainsKey(0)) return [];

        var consecutive = new List<PageResponse<RecipeDto>>();
        while (pages.TryGetValue(consecutive.Count, out var page)) consecutive.Add(page);
        var known = consecutive.SelectMany(item => item.Data).ToList();
        var previous = known.FirstOrDefault(recipe => recipe.Id == (mutation.Before?.Id ?? mutation.After?.Id));
        if (before)
        {
            var id = mutation.Before!.Id;
            known.RemoveAll(recipe => recipe.Id == id);
        }
        if (after)
        {
            var id = mutation.After!.Id;
            known.RemoveAll(recipe => recipe.Id == id);
            var replacement = PublicRecipe(mutation.Recipe!, origin);
            var index = known.FindIndex(recipe => Compare(replacement, recipe) < 0);
            if (index < 0) known.Add(replacement);
            else known.Insert(index, replacement);
        }

        var count = consecutive[0].TotalSize + (after ? 1 : 0) - (before ? 1 : 0);
        var updated = new Dictionary<int, PageResponse<RecipeDto>>();
        foreach (var original in consecutive)
        {
            var offset = (long)original.PageIndex * query.PageSize;
            var length = (int)Math.Clamp(count - offset, 0, query.PageSize);
            if (offset > known.Count || known.Count - offset < length) continue;
            updated[original.PageIndex] = new PageResponse<RecipeDto>
            {
                Data = known.Skip((int)offset).Take(length).ToArray(),
                TotalSize = count,
                PageIndex = original.PageIndex,
                PageSize = original.PageSize
            };
        }
        return updated;
    }

    private static int Compare(RecipeDto left, RecipeDto right)
    {
        var count = right.LikeCount.CompareTo(left.LikeCount);
        if (count != 0) return count;
        var date = right.CreationDate.CompareTo(left.CreationDate);
        return date != 0 ? date : left.Id.CompareTo(right.Id);
    }

    internal static RecipeDto PublicRecipe(RecipeDto source, string origin) => new()
    {
        Id = source.Id,
        UserId = source.UserId,
        UserName = source.UserName,
        UserImageUrl = Rebase(source.UserImageUrl, origin),
        Name = source.Name,
        Description = source.Description,
        IsDraft = source.IsDraft,
        IsAIGeneratedImage = source.IsAIGeneratedImage,
        CookingTimeMinutes = source.CookingTimeMinutes,
        Calories = source.Calories,
        CategoryId = source.CategoryId,
        FileUrl = Rebase(source.FileUrl, origin),
        ViewCount = source.ViewCount,
        LikeCount = source.LikeCount,
        CreationDate = source.CreationDate
    };

    internal static FeedRecipeDto ForViewer(FeedRecipeDto source, FeedRecipeDto? previous, Guid? userId, string origin) => new()
    {
        Id = source.Id, UserId = source.UserId, UserName = source.UserName,
        UserImageUrl = Rebase(source.UserImageUrl, origin),
        IsFollowed = previous?.IsFollowed ?? (userId == source.UserId && source.IsFollowed),
        Name = source.Name, Description = source.Description, IsDraft = source.IsDraft,
        IsAIGeneratedImage = source.IsAIGeneratedImage, CookingTimeMinutes = source.CookingTimeMinutes,
        Calories = source.Calories,
        CategoryId = source.CategoryId, FileUrl = Rebase(source.FileUrl, origin),
        ViewCount = source.ViewCount, LikeCount = source.LikeCount, CreationDate = source.CreationDate,
        IsLiked = previous?.IsLiked ?? (userId == source.UserId && source.IsLiked),
        IsSaved = previous?.IsSaved ?? (userId == source.UserId && source.IsSaved),
        IsSavedAt = previous?.IsSavedAt ?? (userId == source.UserId ? source.IsSavedAt : null)
    };

    internal static string? Rebase(string? url, string origin)
    {
        if (url == null) return null;
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        return uri.IsAbsoluteUri ? new Uri(new Uri(origin), uri.PathAndQuery).ToString() : new Uri(new Uri(origin), uri).ToString();
    }
}
