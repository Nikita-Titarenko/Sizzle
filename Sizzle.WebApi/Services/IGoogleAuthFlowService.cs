using Microsoft.AspNetCore.Mvc;

namespace Sizzle.WebApi.Services;

public interface IGoogleAuthFlowService
{
    IActionResult Start(HttpContext context);

    Task<IActionResult> CompleteAsync(HttpContext context, string? code, string? state, string? error,
        CancellationToken cancellationToken);
}