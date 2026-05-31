using Eventiq.PaymentService.Application.Dto;

namespace Eventiq.PaymentService.Application.Service.Interface;

public interface IAnalyticsService
{
    Task<AdminOverviewDto> GetAdminOverviewAsync();
    Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months = 12);
    Task<List<TopOrgDto>> GetTopOrgsAsync(int top = 10);
    Task<OrgAnalyticsOverviewDto> GetOrgAnalyticsAsync(Guid orgId);
}
