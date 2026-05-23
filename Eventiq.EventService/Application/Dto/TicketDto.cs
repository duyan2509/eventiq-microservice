using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Application.Dto;

public record TicketResponse(
    Guid Id,
    Guid OrderId,
    Guid SessionId,
    string SeatLabel,
    string LegendName,
    decimal Price,
    string QRCode,
    bool IsCheckedIn,
    DateTime? CheckedInAt,
    DateTime IssuedAt)
{
    public static TicketResponse From(Ticket t) => new(
        t.Id, t.OrderId, t.SessionId, t.SeatLabel, t.LegendName,
        t.Price, t.QRCode, t.IsCheckedIn, t.CheckedInAt, t.IssuedAt);
}
