using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Other;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class IngredientsController(IIngredientService ingredientService) : BaseController
{
    /// <summary>Get ingredients with search and pagination.</summary>
    /// <remarks>ErrorKey.BadRequest — invalid pagination parameters.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IngredientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<IngredientListItemDto>>> GetAll([FromQuery] SearchPaginationRequestModel request)
    {
        var ingredients = await ingredientService.GetAllAsync(new GetAllIngredientsDto
        {
            Search = request.Search,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            LanguageCode = GetLanguageCode()
        });

        return Ok(ingredients);
    }
}
