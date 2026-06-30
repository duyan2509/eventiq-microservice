using Eventiq.EventService.Application.Dto;
using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Application.Service.Interface;

public interface ITicketService
{
    Task<List<Ticket>> IssueAsync(Guid orderId, Guid sessionId, List<(Guid SeatId, string SeatLabel, string LegendName, decimal Price)> seats);
    Task<List<Ticket>> GetByOrderAsync(Guid orderId);
    Task<List<EventCheckInItem>> GetCheckedInByEventAsync(Guid eventId);
    Task<List<OrgCheckInItem>> GetCheckedInByOrgAsync(Guid orgId);
    Task<Ticket> CheckInAsync(string signedToken, Guid staffUserId);
}

public record EventCheckInItem(
    Guid TicketId,
    string SeatLabel,
    string LegendName,
    decimal Price,
    string SessionName,
    DateTime SessionStart,
    DateTime CheckedInAt);
