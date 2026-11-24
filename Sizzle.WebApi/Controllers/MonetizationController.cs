using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Monetization;
using Sizzle.Application.Services.Monetization;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Base;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class MonetizationController(IMonetizationService monetizationService, ILogger<BaseController> logger) : BaseController(logger)
{
    [HttpPost("stripe/webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var result = await monetizationService.ProcessWebhookAsync(payload, Request.Headers["Stripe-Signature"].ToString(), cancellationToken);
        return result.IsSuccess ? Ok() : HandleErrors(result);
    }

    [HttpGet("connected-account/status")]
    [ProducesResponseType(typeof(ConnectedAccountStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectedAccountStatusDto>> ConnectedAccountStatus(CancellationToken cancellationToken)
    {
        var result = await monetizationService.GetConnectedAccountStatusAsync(GetUserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleErrors(result);
    }

    [HttpGet("earnings")]
    [ProducesResponseType(typeof(PageResponse<AdEarningDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PageResponse<AdEarningDto>>> Earnings([FromQuery] PaginationRequestModel request,
        CancellationToken cancellationToken)
    {
        var page = await monetizationService.GetEarningsAsync(GetUserId(), request.PageIndex, request.PageSize, cancellationToken);
        return Ok(page);
    }

    [HttpPost("dev/process-ad-earnings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ProcessAdEarnings([FromServices] IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        await monetizationService.ProcessAdEarningsAsync(cancellationToken);
        return Ok();
    }
}
