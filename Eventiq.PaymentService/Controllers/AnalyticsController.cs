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
        => Ok(await _analytics.GetOrgAnalyticsAsync(orgId));
}
