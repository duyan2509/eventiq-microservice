using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.OrganizationService.Controllers;

[ApiController]
[Route("api/organizations/{orgId:guid}/payment")]
[Authorize(Roles = nameof(AppRoles.Organization))]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates Stripe Connect onboarding — creates a connected account and returns the onboarding URL.
    /// </summary>
    [HttpPost("connect")]
    public async Task<ActionResult<PaymentConnectResponse>> ConnectStripeAccount(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _paymentService.ConnectStripeAccountAsync(userId, orgId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Handles the return from Stripe onboarding — verifies the account and publishes PaymentConfigured.
    /// </summary>
    [HttpGet("callback")]
    public async Task<ActionResult<PaymentStatusResponse>> HandleOnboardingCallback(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _paymentService.HandleOnboardingCallbackAsync(userId, orgId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the current payment configuration status for the organization.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaymentStatusResponse>> GetPaymentStatus(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _paymentService.GetPaymentStatusAsync(userId, orgId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Disconnects the Stripe account from the organization.
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<ActionResult> DisconnectStripeAccount(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _paymentService.DisconnectStripeAccountAsync(userId, orgId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");
        return userId;
    }
}
