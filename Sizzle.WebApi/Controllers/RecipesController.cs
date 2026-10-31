using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Recipes;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class RecipesController(IRecipeService recipeService, IMapper mapper) : BaseController
{
    /// <summary>Get a page of recipes with search and filters.</summary>
    /// <remarks>ErrorKey.BadRequest — invalid pagination parameters.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<RecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResponse<RecipeDto>>> GetRecipes([FromQuery] GetRecipesRequestModel request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public, max-age=15";
        var dto = mapper.Map(request, new GetRecipesDto
        {
            OwnerId = request.UserId
        });
        var page = await recipeService.GetRecipesAsync(dto, cancellationToken);

        return Ok(page);
    }

    [HttpGet("personalized")]
    [ProducesResponseType(typeof(PageResponse<FeedRecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResponse<FeedRecipeDto>>> GetPersonalizedRecipes([FromQuery] GetRecipesRequestModel request, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            var page = await recipeService.GetRecipesAsync(mapper.Map(request, new GetRecipesDto { OwnerId = request.UserId }), cancellationToken);
            return Ok(new PageResponse<FeedRecipeDto>
            {
                Data = page.Data.Select(recipe => new FeedRecipeDto
                {
                    Id = recipe.Id, UserId = recipe.UserId, UserName = recipe.UserName,
                    UserImageUrl = recipe.UserImageUrl, Name = recipe.Name, Description = recipe.Description,
                    CookingTimeMinutes = recipe.CookingTimeMinutes, CategoryId = recipe.CategoryId,
                    FileUrl = recipe.FileUrl, ViewCount = recipe.ViewCount, LikeCount = recipe.LikeCount,
                    CreationDate = recipe.CreationDate
                }).ToArray(),
                TotalSize = page.TotalSize, PageIndex = page.PageIndex,
                PageSize = page.PageSize
            });
        }

        var dto = mapper.Map(request, new GetRecipesDto { ApplicationUserId = GetUserId() });
        return Ok(await recipeService.GetPersonalizedRecipesAsync(dto, cancellationToken));
    }

    /// <summary>Get the current user's recipes with filters.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — invalid pagination parameters.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(PageResponse<RecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PageResponse<RecipeDto>>> GetMyRecipes([FromQuery] GetRecipesRequestModel request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var dto = mapper.Map(request, new GetRecipesDto { ApplicationUserId = userId, OwnerId = userId });
        var page = await recipeService.GetRecipesAsync(dto, cancellationToken);

        return Ok(page);
    }

    [HttpGet("saved")]
    [Authorize]
    [ProducesResponseType(typeof(PageResponse<FeedRecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PageResponse<FeedRecipeDto>>> GetSavedRecipes([FromQuery] GetRecipesRequestModel request, CancellationToken cancellationToken)
    {
        return Ok(await recipeService.GetSavedRecipesAsync(mapper.Map(request,
            new GetRecipesDto { ApplicationUserId = GetUserId(), OwnerId = request.UserId }), cancellationToken));
    }

    /// <summary>Create a recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — invalid cooking time, media, ingredient name, unit, quantity or ID, nested parent ingredient, or form input.</para>
    /// <para>ErrorKey.NotFound — the category, ingredient, or parent ingredient does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDto>> CreateRecipe([FromForm] CreateRecipeRequestModel request, CancellationToken cancellationToken)
    {
        var result = await recipeService.CreateRecipeAsync(mapper.Map<CreateRecipeDto>(request,
            options => options.Items["ApplicationUserId"] = GetUserId()), cancellationToken);

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Update the current user's recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — the recipe belongs to another user, or the cooking time, media, ingredient, unit, quantity, nested parent ingredient, or form input is invalid.</para>
    /// <para>ErrorKey.NotFound — the recipe, category, ingredient, or parent ingredient does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPut("{recipeId:guid}")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDto>> UpdateRecipe(Guid recipeId, [FromForm] UpdateRecipeRequestModel request, CancellationToken cancellationToken)
    {
        var result = await recipeService.UpdateRecipeAsync(mapper.Map<UpdateRecipeDto>(request, options =>
        {
            options.Items["RecipeId"] = recipeId;
            options.Items["ApplicationUserId"] = GetUserId();
        }), cancellationToken);

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Delete the current user's recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the recipe does not exist.</para>
    /// <para>ErrorKey.BadRequest — the recipe belongs to another user.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpDelete("{recipeId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteRecipe(Guid recipeId, CancellationToken cancellationToken)
    {
        var result = await recipeService.DeleteRecipeAsync(recipeId, GetUserId(), cancellationToken);

        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    /// <summary>Get recipe details.</summary>
    /// <remarks>ErrorKey.NotFound — the recipe does not exist.</remarks>
    [HttpGet("{recipeId:guid}")]
    [ProducesResponseType(typeof(RecipeDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDetailsDto>> GetRecipeById(Guid recipeId, CancellationToken cancellationToken)
    {
        var result = await recipeService.GetRecipeByIdAsync(new GetRecipeByIdDto
        {
            Id = recipeId,
            ApplicationUserId = User.Identity?.IsAuthenticated == true ? GetUserId() : null,
            LanguageCode = GetLanguageCode()
        }, cancellationToken);

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Like or unlike a recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the recipe does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost("{recipeId:guid}/toggle-like")]
    [Authorize]
    [ProducesResponseType(typeof(RecipeLikeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeLikeDto>> ToggleLike(Guid recipeId, CancellationToken cancellationToken)
    {
        var result = await recipeService.ToggleLikeAsync(new ToggleLikeRecipeDto
        {
            RecipeId = recipeId,
            ApplicationUserId = GetUserId()
        }, cancellationToken);

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost("{recipeId:guid}/toggle-save")]
    [Authorize]
    [ProducesResponseType(typeof(RecipeSaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeSaveDto>> ToggleSave(Guid recipeId, CancellationToken cancellationToken)
    {
        var result = await recipeService.ToggleSaveAsync(recipeId, GetUserId(), cancellationToken);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost("{recipeId:guid}/update-share")]
    [Authorize]
    [ProducesResponseType(typeof(RecipeShareDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeShareDto>> UpdateShare(Guid recipeId, CancellationToken cancellationToken)
    {
        var result = await recipeService.UpdateShareAsync(recipeId, GetUserId(), cancellationToken);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Record a recipe view if the update interval has elapsed.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the recipe does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost("{recipeId:guid}/update-view")]
    [Authorize]
    [ProducesResponseType(typeof(FeedRecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedRecipeDto>> UpdateView(Guid recipeId, CancellationToken cancellationToken)
    {
        var result = await recipeService.UpdateViewAsync(new UpdateRecipeViewDto
        {
            RecipeId = recipeId,
            ApplicationUserId = GetUserId(),
            LanguageCode = GetLanguageCode()
        }, cancellationToken);

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

}
