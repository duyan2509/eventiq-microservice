namespace Eventiq.PaymentService.Application.Dto;

public record AdminOverviewDto(
    decimal TotalRevenue,
    decimal TotalPlatformFee,
    int TotalOrders,
    int TotalOrgs);

public record MonthlyRevenueDto(string Month, decimal Revenue, decimal PlatformFee);

public record TopOrgDto(Guid OrgId, string EventName, decimal Revenue, decimal PlatformFee, int Orders);

public record OrgAnalyticsOverviewDto(
    decimal TotalRevenue,
    decimal TotalPlatformFee,
    decimal NetRevenue,
    int TotalOrders,
    List<OrgEventRevenueDto> ByEvent);

public record OrgEventRevenueDto(
    string EventName,
    string SessionName,
    int Tickets,
    decimal Revenue,
    decimal PlatformFee);
