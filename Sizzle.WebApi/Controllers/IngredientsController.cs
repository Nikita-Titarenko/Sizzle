using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Other;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class IngredientsController(IIngredientService ingredientService, IMapper mapper, ILogger<BaseController> logger) : BaseController(logger)
{
    /// <summary>Get ingredients with search and pagination.</summary>
    /// <remarks>ErrorKey.BadRequest — invalid pagination parameters.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IngredientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<IngredientListItemDto>>> GetAll([FromQuery] SearchPaginationRequestModel request, CancellationToken cancellationToken)
    {
        var ingredients = await ingredientService.GetAllAsync(mapper.Map(request,
            new GetAllIngredientsDto { LanguageCode = GetLanguageCode() }), cancellationToken);

        return Ok(ingredients);
    }
}
