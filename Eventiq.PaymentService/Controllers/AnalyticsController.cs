using System.Security.Claims;
using Eventiq.PaymentService.Application.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.PaymentService.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    public AnalyticsController(IAnalyticsService analytics) => _analytics = analytics;

    [HttpGet("admin/overview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminOverview()
        => Ok(await _analytics.GetAdminOverviewAsync());

    [HttpGet("admin/revenue-by-month")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int months = 12)
        => Ok(await _analytics.GetMonthlyRevenueAsync(months));

    [HttpGet("admin/top-orgs")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetTopOrgs([FromQuery] int top = 10)
        => Ok(await _analytics.GetTopOrgsAsync(top));

    [HttpGet("org/{orgId:guid}")]
    [Authorize(Roles = "Organization,Staff")]
    public async Task<IActionResult> GetOrgAnalytics(Guid orgId)
    {
        // Role alone is not enough: ensure the caller belongs to the org they
        // ask for. The orgId in the signed JWT is the source of truth, not the
        // route param — otherwise any Org/Staff user could read another org's
        // revenue by swapping the GUID in the URL.
        var tokenOrgId = User.FindFirstValue("orgId");
        if (!Guid.TryParse(tokenOrgId, out var parsed) || parsed != orgId)
            return Forbid();

        return Ok(await _analytics.GetOrgAnalyticsAsync(orgId));
    }
}
