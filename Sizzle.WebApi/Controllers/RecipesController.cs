using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;
using Sizzle.WebApi.Requests.Recipes;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class RecipesController(IRecipeService recipeService) : BaseController
{
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<RecipeDto>), StatusCodes.Status200OK)]
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
            ApplicationUserId = userId == Guid.Empty ? null : userId
        });

        return Ok(page);
    }

    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(PageResponse<RecipeDto>), StatusCodes.Status200OK)]
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

    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
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
            IngredientIds = request.IngredientIds,
            NewIngredientNames = request.NewIngredientNames
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPut("{recipeId:guid}")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
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
            IngredientIds = request.IngredientIds,
            NewIngredientNames = request.NewIngredientNames
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpDelete("{recipeId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteRecipe(Guid recipeId)
    {
        var result = await recipeService.DeleteRecipeAsync(recipeId, GetUserId());

        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }

    [HttpGet("{recipeId:guid}")]
    [ProducesResponseType(typeof(RecipeDetailsDto), StatusCodes.Status200OK)]
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

    [HttpPost("{recipeId:guid}/toggle-like")]
    [Authorize]
    [ProducesResponseType(typeof(RecipeLikeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecipeLikeDto>> ToggleLike(Guid recipeId)
    {
        var result = await recipeService.ToggleLikeAsync(new ToggleLikeRecipeDto
        {
            RecipeId = recipeId,
            ApplicationUserId = GetUserId()
        });

        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost("{recipeId:guid}/update-view")]
    [Authorize]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
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
