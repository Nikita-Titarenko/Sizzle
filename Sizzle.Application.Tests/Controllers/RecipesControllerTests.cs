using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;
using Sizzle.WebApi.Controllers;
using Sizzle.WebApi.Requests.Recipes;

namespace Sizzle.Application.Tests.Controllers;

public class RecipesControllerTests
{
    [Fact]
    public void NewIngredient_RequiresUnitInMultipartForm()
    {
        var property = typeof(NewIngredientRequestModel).GetProperty(nameof(NewIngredientRequestModel.Unit));

        Assert.NotNull(property);
        Assert.NotNull(Attribute.GetCustomAttribute(property, typeof(BindRequiredAttribute)));
    }

    [Fact]
    public void NewIngredient_TranslationsAreNotClientInput()
    {
        Assert.Null(typeof(NewIngredientRequestModel).GetProperty("Translations"));
        Assert.Null(typeof(NewIngredientDto).GetProperty("Translations"));
    }

    private readonly IRecipeService recipes = Substitute.For<IRecipeService>();
    private readonly Guid userId = Guid.NewGuid();

    [Theory]
    [InlineData("\"Grams\"", IngredientUnit.Grams)]
    [InlineData("0", IngredientUnit.Grams)]
    [InlineData("\"Milliliters\"", IngredientUnit.Milliliters)]
    [InlineData("1", IngredientUnit.Milliliters)]
    [InlineData("\"Pieces\"", IngredientUnit.Pieces)]
    [InlineData("2", IngredientUnit.Pieces)]
    public void GeneratedRecipe_DeserializesStringAndNumberUnits(string unitJson, IngredientUnit expected)
    {
        var json = $$"""{"name":"Test","ingredients":[{"name":"Ingredient","unit":{{unitJson}},"quantity":1}]}""";

        var recipe = JsonSerializer.Deserialize<GeneratedRecipeDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(expected, Assert.Single(recipe!.Ingredients).Unit);
        Assert.Contains($"\"unit\":\"{expected}\"", JsonSerializer.Serialize(recipe, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private RecipesController CreateController()
    {
        var controller = new RecipesController(recipes, Substitute.For<IMapper>(), NullLogger<BaseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
                }
            }
        };
        controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
        return controller;
    }

    [Fact]
    public async Task UpdateView_ReturnsNoContentOnSuccess()
    {
        recipes.UpdateViewAsync(Arg.Any<UpdateRecipeViewDto>(), Arg.Any<CancellationToken>())
            .Returns(new Result());

        var result = await CreateController().UpdateView(Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UpdateView_ReturnsNotFoundOnFailure()
    {
        recipes.UpdateViewAsync(Arg.Any<UpdateRecipeViewDto>(), Arg.Any<CancellationToken>())
            .Returns(new Result { Errors = [new Error { Key = ErrorKey.NotFound, Message = "Recipe not found" }] });

        var result = await CreateController().UpdateView(Guid.NewGuid(), default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GenerateRecipeImage_RequiresSubscription()
    {
        recipes.GenerateRecipeImageAsync(Arg.Any<GeneratedRecipeDto>(), userId, Arg.Any<CancellationToken>())
            .Returns(new Result<GeneratedRecipeImage>
            {
                Errors = [new Error { Key = ErrorKey.Forbidden, Message = "An active subscription is required" }]
            });
        var result = await CreateController().GenerateRecipeImage(new GeneratedRecipeDto { Name = "Pasta" }, default);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(ErrorKey.Forbidden, Assert.Single(Assert.IsType<List<Error>>(forbidden.Value)).Key);
        await recipes.Received(1).GenerateRecipeImageAsync(Arg.Any<GeneratedRecipeDto>(), userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRecipeFromVideo_RequiresSubscription()
    {
        recipes.GenerateRecipeFromVideoAsync(Arg.Any<GenerateRecipeFromVideoRequestDto>(), userId, Arg.Any<CancellationToken>())
            .Returns(new Result<IAsyncEnumerable<RecipeGenerationEvent>>
            {
                Errors = [new Error { Key = ErrorKey.Forbidden, Message = "An active subscription is required" }]
            });
        var result = await CreateController().GenerateRecipeFromVideo(null, default);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(ErrorKey.Forbidden, Assert.Single(Assert.IsType<List<Error>>(forbidden.Value)).Key);
        await recipes.Received(1).GenerateRecipeFromVideoAsync(Arg.Any<GenerateRecipeFromVideoRequestDto>(), userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRecipeFromVideo_StreamsForSubscriber()
    {
        recipes.GenerateRecipeFromVideoAsync(Arg.Any<GenerateRecipeFromVideoRequestDto>(), userId, Arg.Any<CancellationToken>())
            .Returns(new Result<IAsyncEnumerable<RecipeGenerationEvent>> { Value = VideoEvents() });
        var controller = CreateController();
        controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
        controller.Response.Body = new MemoryStream();

        var result = await controller.GenerateRecipeFromVideo(null, default);

        Assert.IsType<EmptyResult>(result);
        Assert.Equal("text/event-stream", controller.Response.ContentType);
        controller.Response.Body.Position = 0;
        var output = await new StreamReader(controller.Response.Body).ReadToEndAsync();
        Assert.Contains("event: chunk", output);
    }

    private static async IAsyncEnumerable<RecipeGenerationEvent> VideoEvents()
    {
        await Task.Yield();
        yield return new RecipeGenerationEvent("chunk", Text: "Recipe");
    }

    [Fact]
    public async Task GenerateRecipeImage_ReturnsGeneratedImageForSubscriber()
    {
        var image = new GeneratedRecipeImage([1, 2, 3], "image/png", true);
        recipes.GenerateRecipeImageAsync(Arg.Any<GeneratedRecipeDto>(), userId, Arg.Any<CancellationToken>())
            .Returns(new Result<GeneratedRecipeImage> { Value = image });

        var controller = CreateController();
        var result = await controller.GenerateRecipeImage(new GeneratedRecipeDto { Name = "Pasta" }, default);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(image.Data, file.FileContents);
        Assert.False(controller.Response.Headers.ContainsKey("X-Is-AI-Generated-Image"));
    }
}