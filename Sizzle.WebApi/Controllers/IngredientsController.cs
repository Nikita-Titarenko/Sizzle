using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Ingredients;
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

    /// <summary>Recalculate ingredient quantities when one ingredient's quantity changes.</summary>
    /// <remarks>Scales every other ingredient proportionally and rounds to the nearest whole number, with halves rounded up. ErrorKey.BadRequest — invalid quantities, duplicate IDs, missing changed ingredient, or overflow.</remarks>
    /// <param name="request">Initial ingredient IDs and quantities, plus the changed ingredient and its new quantity.</param>
    /// <returns>The recalculated quantities in the same order as the initial ingredients.</returns>
    [HttpGet("proportions")]
    [ProducesResponseType(typeof(IEnumerable<IngredientProportionItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<IngredientProportionItemDto>> CalculateProportions([FromBody] CalculateIngredientProportionsRequestModel request)
    {
        var result = ingredientService.CalculateProportions(mapper.Map<CalculateIngredientProportionsDto>(request));
        return result.IsSuccess ? Ok(result.Value) : HandleErrors(result);
    }
}
