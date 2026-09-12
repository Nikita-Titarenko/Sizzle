using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Services.Ingredients;
using Sizzle.WebApi.Requests.Other;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class IngredientsController(IIngredientService ingredientService) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<IngredientListItemDto>>> GetAll([FromQuery] SearchPaginationRequestModel request)
    {
        var ingredients = await ingredientService.GetAllAsync(new GetAllIngredientsDto
        {
            Search = request.Search,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });

        return Ok(ingredients);
    }
}
