using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Domain.Common;

namespace Sizzle.WebApi.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
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
            ErrorKey.Banned => StatusCode(StatusCodes.Status403Forbidden, result.Errors),
            _ => BadRequest(result.Errors)
        };
    }

    protected Guid GetUserId()
    {
        var userId = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userId, out var parsedId) ? parsedId : Guid.Empty;
    }
}
