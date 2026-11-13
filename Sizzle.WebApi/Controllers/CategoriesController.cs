using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Services.Categories;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class CategoriesController(ICategoryService categoryService, ILogger<BaseController> logger) : BaseController(logger)
{
    /// <summary>Get categories in the Accept-Language locale (English by default).</summary>
    /// <remarks>ErrorKey: none.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetAllAsync(GetLanguageCode(), cancellationToken));
    }
}