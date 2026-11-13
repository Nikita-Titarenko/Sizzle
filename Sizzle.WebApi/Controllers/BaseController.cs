using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Serialization;

namespace Sizzle.WebApi.Controllers;

[ApiController]
public abstract class BaseController(ILogger<BaseController> logger) : ControllerBase
{
    protected async Task WriteGenerationStreamAsync(IAsyncEnumerable<RecipeGenerationEvent> events, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        try
        {
            await foreach (var item in events.WithCancellation(cancellationToken))
            {
                await WriteGenerationEventAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Generation stream failed");
            if (!cancellationToken.IsCancellationRequested)
            {
                await WriteGenerationEventAsync(new RecipeGenerationEvent("error", Errors:
                    [new Error { Message = "Generation failed", Key = ErrorKey.UnexpectedError }]), cancellationToken);
            }
        }
    }

    protected async Task WriteGenerationEventAsync(RecipeGenerationEvent item, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"event: {item.Type}\ndata: {JsonSerializer.Serialize(item, StreamJsonOptions.Value)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    protected ActionResult HandleErrors(Result result)
    {
        var error = result.Errors.FirstOrDefault();
        if (error == null)
        {
            return BadRequest();
        }

        return error.Key switch
        {
            ErrorKey.NotFound => NotFound(result.Errors),
            ErrorKey.AlreadyExists => Conflict(result.Errors),
            ErrorKey.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Errors),
            _ => BadRequest(result.Errors)
        };
    }

    protected Guid GetUserId()
    {
        var userId = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userId, out var parsedId) ? parsedId : Guid.Empty;
    }

    protected string GetLanguageCode()
    {
        var language = Request.GetTypedHeaders().AcceptLanguage?
            .Where(value => value.Quality is null or > 0)
            .OrderByDescending(value => value.Quality ?? 1)
            .Select(value => value.Value.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != "*");

        return language?.Split('-', 2)[0].ToLowerInvariant() ?? "en";
    }
}
