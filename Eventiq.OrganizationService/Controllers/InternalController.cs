using Eventiq.OrganizationService.Domain.Enum;
using Eventiq.OrganizationService.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.OrganizationService.Controllers;

/// <summary>
/// Internal endpoints — only called by other microservices, NOT exposed through API Gateway.
/// </summary>
[ApiController]
[Route("internal/organizations")]
public class InternalController : ControllerBase
{
    private readonly IOrganizationRepository _orgRepo;
    private readonly ILogger<InternalController> _logger;

    public InternalController(IOrganizationRepository orgRepo, ILogger<InternalController> logger)
    {
        _orgRepo = orgRepo;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether the given organization has an active (fully configured) Stripe payment account.
    /// Called by EventService before allowing event submission.
    /// </summary>
    [HttpGet("{orgId:guid}/payment-status")]
    public async Task<ActionResult<PaymentStatusResult>> GetPaymentStatus(Guid orgId, CancellationToken ct)
    {
        var org = await _orgRepo.GetByIdAsync(orgId, ct);
        if (org == null)
            return NotFound();

        var isActive = org.PaymentStatus == PaymentStatus.Configured;
        _logger.LogInformation("Internal payment-status check for org {OrgId}: IsActive={IsActive}", orgId, isActive);

        return Ok(new PaymentStatusResult(isActive));
    }
}

public record PaymentStatusResult(bool IsActive);
