using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Controllers;
using Sizzle.WebApi.Requests.Ingredients;

namespace Sizzle.Application.Tests.Controllers;

public class IngredientsControllerTests
{
    private readonly IIngredientService service = Substitute.For<IIngredientService>();
    private readonly IMapper mapper = Substitute.For<IMapper>();

    [Fact]
    public void CalculateProportions_ReturnsScaledIngredients()
    {
        var request = new CalculateIngredientProportionsRequestModel();
        var dto = new CalculateIngredientProportionsDto();
        var calculated = new List<IngredientProportionItemDto> { new() { Id = Guid.NewGuid(), Quantity = 1000 } };
        mapper.Map<CalculateIngredientProportionsDto>(request).Returns(dto);
        service.CalculateProportions(dto).Returns(new Result<IEnumerable<IngredientProportionItemDto>> { Value = calculated });

        var result = new IngredientsController(service, mapper, NullLogger<BaseController>.Instance).CalculateProportions(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(calculated, ok.Value);
        service.Received(1).CalculateProportions(dto);
    }

    [Fact]
    public void CalculateProportions_ReturnsBadRequestOnInvalidProportions()
    {
        var request = new CalculateIngredientProportionsRequestModel();
        var dto = new CalculateIngredientProportionsDto();
        mapper.Map<CalculateIngredientProportionsDto>(request).Returns(dto);
        service.CalculateProportions(dto).Returns(new Result<IEnumerable<IngredientProportionItemDto>>
        {
            Errors = [new Error { Key = ErrorKey.BadRequest, Message = "Invalid ingredient quantities" }]
        });

        var result = new IngredientsController(service, mapper, NullLogger<BaseController>.Instance).CalculateProportions(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}