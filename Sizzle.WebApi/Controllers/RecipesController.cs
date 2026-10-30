using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Recipes;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class RecipesController(IRecipeService recipeService) : BaseController
{
    /// <summary>Get a page of recipes with search and filters.</summary>
    /// <remarks>ErrorKey.BadRequest — invalid pagination parameters.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<RecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResponse<RecipeDto>>> GetRecipes([FromQuery] GetRecipesRequestModel request)
    {
        var userId = User.Identity?.IsAuthenticated == true ? GetUserId() : Guid.Empty;
        var page = await recipeService.GetRecipesAsync(new GetRecipesDto
        {
            IngredientIds = request.IngredientIds,
            CategoryIds = request.CategoryIds,
            Search = request.Search,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            ApplicationUserId = userId == Guid.Empty ? null : userId,
            OwnerId = request.UserId
        });

        return Ok(page);
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
    public async Task<ActionResult<PageResponse<RecipeDto>>> GetMyRecipes([FromQuery] GetRecipesRequestModel request)
    {
        var userId = GetUserId();
        var page = await recipeService.GetRecipesAsync(new GetRecipesDto
        {
            IngredientIds = request.IngredientIds,
            CategoryIds = request.CategoryIds,
            Search = request.Search,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            ApplicationUserId = userId,
            OwnerId = userId
        });

        return Ok(page);
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
    public async Task<ActionResult<RecipeDto>> CreateRecipe([FromForm] CreateRecipeRequestModel request)
    {
        var result = await recipeService.CreateRecipeAsync(new CreateRecipeDto
        {
            ApplicationUserId = GetUserId(),
            Name = request.Name,
            Description = request.Description,
            CookingTimeMinutes = request.CookingTimeMinutes,
            CategoryId = request.CategoryId,
            File = ToUpload(request.File),
            Steps = request.Steps.Select(step => new RecipeStepInputDto
            {
                Name = step.Name,
                Description = step.Description,
                Files = step.Files.Select(file => ToUpload(file)!).ToArray()
            }).ToArray(),
            Ingredients = request.Ingredients.Select(ingredient => new RecipeIngredientInputDto
            {
                Id = ingredient.Id,
                Quantity = ingredient.Quantity
            }).ToArray(),
            NewIngredients = request.NewIngredients.Select(ingredient => new NewIngredientDto
            {
                Name = ingredient.Name,
                ParentId = ingredient.ParentId,
                Unit = ingredient.Unit,
                Quantity = ingredient.Quantity
            }).ToArray()
        });

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
    public async Task<ActionResult<RecipeDto>> UpdateRecipe(Guid recipeId, [FromForm] UpdateRecipeRequestModel request)
    {
        var result = await recipeService.UpdateRecipeAsync(new UpdateRecipeDto
        {
            Id = recipeId,
            ApplicationUserId = GetUserId(),
            Name = request.Name,
            Description = request.Description,
            CookingTimeMinutes = request.CookingTimeMinutes,
            CategoryId = request.CategoryId,
            File = ToUpload(request.File),
            Steps = request.Steps.Select(step => new RecipeStepInputDto
            {
                Name = step.Name,
                Description = step.Description,
                Files = step.Files.Select(file => ToUpload(file)!).ToArray()
            }).ToArray(),
            Ingredients = request.Ingredients.Select(ingredient => new RecipeIngredientInputDto
            {
                Id = ingredient.Id,
                Quantity = ingredient.Quantity
            }).ToArray(),
            NewIngredients = request.NewIngredients.Select(ingredient => new NewIngredientDto
            {
                Name = ingredient.Name,
                ParentId = ingredient.ParentId,
                Unit = ingredient.Unit,
                Quantity = ingredient.Quantity
            }).ToArray()
        });

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
    public async Task<ActionResult> DeleteRecipe(Guid recipeId)
    {
        var result = await recipeService.DeleteRecipeAsync(recipeId, GetUserId());

        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    /// <summary>Get recipe details.</summary>
    /// <remarks>ErrorKey.NotFound — the recipe does not exist.</remarks>
    [HttpGet("{recipeId:guid}")]
    [ProducesResponseType(typeof(RecipeDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDetailsDto>> GetRecipeById(Guid recipeId)
    {
        var result = await recipeService.GetRecipeByIdAsync(new GetRecipeByIdDto
        {
            Id = recipeId,
            ApplicationUserId = User.Identity?.IsAuthenticated == true ? GetUserId() : null,
            LanguageCode = GetLanguageCode()
        });

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
    public async Task<ActionResult<RecipeLikeDto>> ToggleLike(Guid recipeId)
    {
        var result = await recipeService.ToggleLikeAsync(new ToggleLikeRecipeDto
        {
            RecipeId = recipeId,
            ApplicationUserId = GetUserId()
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Record a recipe view if the update interval has elapsed.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the recipe does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost("{recipeId:guid}/update-view")]
    [Authorize]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDto>> UpdateView(Guid recipeId)
    {
        var result = await recipeService.UpdateViewAsync(new UpdateRecipeViewDto
        {
            RecipeId = recipeId,
            ApplicationUserId = GetUserId()
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    private static RecipeMediaUploadDto? ToUpload(IFormFile? file)
    {
        return file == null ? null : new RecipeMediaUploadDto
        {
            FileName = file.FileName,
            Length = file.Length,
            OpenReadStream = file.OpenReadStream
        };
    }
}
