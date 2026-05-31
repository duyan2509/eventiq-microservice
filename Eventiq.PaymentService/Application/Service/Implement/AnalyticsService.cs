using Eventiq.PaymentService.Application.Dto;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Enums;
using Eventiq.PaymentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class AnalyticsService : IAnalyticsService
{
    private readonly PaymentDbContext _db;

    public AnalyticsService(PaymentDbContext db) => _db = db;

    public async Task<AdminOverviewDto> GetAdminOverviewAsync()
    {
        var paid = await _db.Orders
            .Where(o => o.Status == OrderStatus.Paid)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(o => o.TotalAmount),
                Fee = g.Sum(o => o.PlatformFee),
                Orders = g.Count(),
                Orgs = g.Select(o => o.OrgId).Distinct().Count()
            })
            .FirstOrDefaultAsync();

        return new AdminOverviewDto(
            paid?.Revenue ?? 0,
            paid?.Fee ?? 0,
            paid?.Orders ?? 0,
            paid?.Orgs ?? 0);
    }

    public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months = 12)
    {
        var from = DateTime.UtcNow.AddMonths(-months + 1);
        var cutoff = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rows = await _db.Orders
            .Where(o => o.Status == OrderStatus.Paid && o.PaidAt >= cutoff)
            .GroupBy(o => new { o.PaidAt!.Value.Year, o.PaidAt.Value.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Sum(o => o.TotalAmount),
                Fee = g.Sum(o => o.PlatformFee)
            })
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToListAsync();

        // Fill missing months with zeros
        var result = new List<MonthlyRevenueDto>();
        for (var i = months - 1; i >= 0; i--)
        {
            var d = DateTime.UtcNow.AddMonths(-i);
            var match = rows.FirstOrDefault(r => r.Year == d.Year && r.Month == d.Month);
            result.Add(new MonthlyRevenueDto(
                d.ToString("MMM yyyy"),
                match?.Revenue ?? 0,
                match?.Fee ?? 0));
        }
        return result;
    }

    public async Task<List<TopOrgDto>> GetTopOrgsAsync(int top = 10)
    {
        return await _db.Orders
            .Where(o => o.Status == OrderStatus.Paid)
            .GroupBy(o => new { o.OrgId, o.EventName })
            .Select(g => new TopOrgDto(
                g.Key.OrgId,
                g.Key.EventName,
                g.Sum(o => o.TotalAmount),
                g.Sum(o => o.PlatformFee),
                g.Count()))
            .OrderByDescending(x => x.Revenue)
            .Take(top)
            .ToListAsync();
    }

    public async Task<OrgAnalyticsOverviewDto> GetOrgAnalyticsAsync(Guid orgId)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.OrgId == orgId && o.Status == OrderStatus.Paid)
            .ToListAsync();

        var totalRevenue = orders.Sum(o => o.TotalAmount);
        var totalFee = orders.Sum(o => o.PlatformFee);

        var byEvent = orders
            .GroupBy(o => new { o.EventName, o.SessionName })
            .Select(g => new OrgEventRevenueDto(
                g.Key.EventName,
                g.Key.SessionName,
                g.Sum(o => o.Items.Count),
                g.Sum(o => o.TotalAmount),
                g.Sum(o => o.PlatformFee)))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return new OrgAnalyticsOverviewDto(
            totalRevenue,
            totalFee,
            totalRevenue - totalFee,
            orders.Count,
            byEvent);
    }
}
