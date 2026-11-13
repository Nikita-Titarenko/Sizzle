using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Base;
using Sizzle.WebApi.Requests.Recipes;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class RecipesController(IRecipeService recipeService, IMapper mapper, ILogger<BaseController> logger) : BaseController(logger)
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

    /// <summary>Get personalized recipes for the current user, or a public recipe page for anonymous visitors.</summary>
    /// <remarks>ErrorKey.BadRequest — invalid pagination parameters.</remarks>
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
                    IsDraft = recipe.IsDraft,
                    IsAIGeneratedImage = recipe.IsAIGeneratedImage,
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

    /// <summary>Get the current user's draft recipes with filters.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — invalid pagination parameters.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpGet("mine/drafts")]
    [Authorize]
    [ProducesResponseType(typeof(PageResponse<RecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PageResponse<RecipeDto>>> GetMyDraftRecipes([FromQuery] GetRecipesRequestModel request, CancellationToken cancellationToken)
    {
        var dto = mapper.Map(request, new GetRecipesDto { ApplicationUserId = GetUserId() });
        return Ok(await recipeService.GetDraftRecipesAsync(dto, cancellationToken));
    }

    /// <summary>Get the current user's saved recipes with filters.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — invalid pagination parameters.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
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

    /// <summary>Get recipes from followed users in chronological order.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — invalid pagination parameters.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpGet("following")]
    [Authorize]
    [ProducesResponseType(typeof(PageResponse<FeedRecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PageResponse<FeedRecipeDto>>> GetFollowingRecipes([FromQuery] PaginationRequestModel request, CancellationToken cancellationToken)
    {
        return Ok(await recipeService.GetFollowingRecipesAsync(new GetRecipesDto
        {
            ApplicationUserId = GetUserId(),
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        }, cancellationToken));
    }

    /// <summary>Create a recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — invalid cooking time, media, ingredient name, unit, quantity or ID, nested parent ingredient, or form input.</para>
    /// <para>ErrorKey.NotFound — the category, ingredient, or parent ingredient does not exist.</para>
    /// <para>ErrorKey.RecipeRejected — spam, unsafe, implausible or unrelated-media recipe; response contains errors only.</para>
    /// <para>ErrorKey.RecipeNeedsCorrection — ingredient name, alias or parent requires correction; response contains errors and correctedRecipe. Nothing is saved.</para>
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

        if (result.CorrectedRecipe != null)
        {
            return BadRequest(new { errors = result.Errors, correctedRecipe = result.CorrectedRecipe });
        }

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Stream a generated recipe from a text request without saving it.</summary>
    /// <remarks>
    /// <para>Streams chunk, completed, or error events as server-sent events.</para>
    /// <para>ErrorKey.BadRequest — invalid generation request.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost("generate")]
    [Authorize]
    [Consumes("application/json")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public Task GenerateRecipe([FromBody] GenerateRecipeRequestModel request, CancellationToken cancellationToken)
    {
        return WriteGenerationStreamAsync(recipeService.GenerateRecipeAsync(new GenerateRecipeRequestDto
        {
            LanguageCode = GetLanguageCode(), Message = request.Message, Recipe = request.Recipe
        }, cancellationToken), cancellationToken);
    }

    /// <summary>Generate an image of a recipe for a user with an active subscription.</summary>
    /// <remarks>
    /// <para>Returns the generated image as a PNG or JPEG file without saving it.</para>
    /// <para>ErrorKey.BadRequest — the recipe name is missing or the recipe is too long.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// <para>ErrorKey.Forbidden — an active subscription is required.</para>
    /// </remarks>
    [HttpPost("generate-image")]
    [Authorize]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "image/png", "image/jpeg")]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateRecipeImage([FromBody] GeneratedRecipeDto? recipe, CancellationToken cancellationToken)
    {
        var result = await recipeService.GenerateRecipeImageAsync(recipe, GetUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            return HandleErrors(result);
        }

        return File(result.Value!.Data, result.Value.MimeType);
    }

    /// <summary>Stream a generated recipe from a cooking video for a user with an active subscription.</summary>
    /// <remarks>
    /// <para>Streams chunk, completed, or error events as server-sent events.</para>
    /// <para>ErrorKey.BadRequest — missing or unsupported video, or invalid form input.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// <para>ErrorKey.Forbidden — an active subscription is required.</para>
    /// </remarks>
    [HttpPost("generate-from-video")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/event-stream")]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateRecipeFromVideo([FromForm] IFormFile? video, CancellationToken cancellationToken)
    {
        var result = await recipeService.GenerateRecipeFromVideoAsync(new GenerateRecipeFromVideoRequestDto
        {
            Video = video == null ? null : new RecipeMediaUploadDto
            {
                FileName = video.FileName, Length = video.Length, OpenReadStream = video.OpenReadStream
            },
            LanguageCode = GetLanguageCode(),
            FormFieldCount = Request.Form.Keys.Count,
            FileCount = Request.Form.Files.Count
        }, GetUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            return HandleErrors(result);
        }

        await WriteGenerationStreamAsync(result.Value!, cancellationToken);
        return new EmptyResult();
    }

    /// <summary>Update the current user's recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — the recipe belongs to another user, or the cooking time, media, ingredient, unit, quantity, nested parent ingredient, or form input is invalid.</para>
    /// <para>ErrorKey.NotFound — the recipe, category, ingredient, or parent ingredient does not exist.</para>
    /// <para>ErrorKey.RecipeRejected — spam, unsafe, implausible or unrelated-media recipe; response contains errors only.</para>
    /// <para>ErrorKey.RecipeNeedsCorrection — ingredient name, alias or parent requires correction; response contains errors and correctedRecipe. Nothing is saved.</para>
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

        if (result.CorrectedRecipe != null)
        {
            return BadRequest(new { errors = result.Errors, correctedRecipe = result.CorrectedRecipe });
        }

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

    /// <summary>Save or unsave a recipe for the current user.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the recipe does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
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

    /// <summary>Record that the current user shared a recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the recipe does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateView(Guid recipeId, CancellationToken cancellationToken)
    {
        var result = await recipeService.UpdateViewAsync(new UpdateRecipeViewDto
        {
            RecipeId = recipeId,
            ApplicationUserId = GetUserId()
        }, cancellationToken);

        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

}
