using Eventiq.EventService.Application.Service.Interface;
using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Application.Service.Implement;

public class TicketService : ITicketService
{
    public Task<List<Ticket>> IssueAsync(Guid orderId, Guid sessionId, List<(Guid SeatId, string SeatLabel, string LegendName, decimal Price)> seats)
    {
        throw new NotImplementedException();
    }

    public Task<List<Ticket>> GetByOrderAsync(Guid orderId)
    {
        throw new NotImplementedException();
    }

    public Task CheckInAsync(Guid ticketId, Guid staffUserId)
    {
        throw new NotImplementedException();
    }
}
