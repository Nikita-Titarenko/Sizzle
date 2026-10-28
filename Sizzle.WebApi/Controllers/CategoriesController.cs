using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Services.Categories;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class CategoriesController(ICategoryService categoryService) : BaseController
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll()
    {
        return Ok(await categoryService.GetAllAsync(GetLanguageCode()));
    }
}