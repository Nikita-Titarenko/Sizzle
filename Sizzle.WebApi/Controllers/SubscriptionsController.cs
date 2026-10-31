using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Subscriptions;
using Sizzle.Application.Services.Subscriptions;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public sealed class SubscriptionsController(ISubscriptionService subscriptions) : BaseController
{
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SubscriptionPlanDto>> GetPlans(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Ok(subscriptions.GetPlans());
    }

    [HttpPost("{planId:int}/checkout")]
    [Authorize]
    [ProducesResponseType(typeof(SubscriptionCheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SubscriptionCheckoutDto>> Checkout(int planId, CancellationToken cancellationToken)
    {
        var result = await subscriptions.StartCheckoutAsync(GetUserId(), (SubscriptionPlan)planId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleErrors(result);
    }

    [HttpPost("stripe/webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var result = await subscriptions.ProcessWebhookAsync(payload, Request.Headers["Stripe-Signature"].ToString(), cancellationToken);
        return result.IsSuccess ? Ok() : HandleErrors(result);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(SubscriptionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SubscriptionStatusDto>> GetSubscription(CancellationToken cancellationToken)
    {
        return Ok(await subscriptions.GetStatusAsync(GetUserId(), cancellationToken));
    }
}