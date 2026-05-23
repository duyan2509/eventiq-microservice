using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Application.Service.Interface;

public interface ITicketService
{
    Task<List<Ticket>> IssueAsync(Guid orderId, Guid sessionId, List<(Guid SeatId, string SeatLabel, string LegendName, decimal Price)> seats);
    Task<List<Ticket>> GetByOrderAsync(Guid orderId);
    Task CheckInAsync(Guid ticketId, Guid staffUserId);
}
