using Eventiq.EventService.Application.Dto;
using Eventiq.EventService.Application.Service.Interface;
using Eventiq.EventService.Application.Utility;
using Eventiq.EventService;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.EventService.Application.Service.Implement;

public class TicketService : ITicketService
{
    private readonly EvtEventDbContext _dbContext;
    private readonly TicketSigner _signer;

    public TicketService(EvtEventDbContext dbContext, TicketSigner signer)
    {
        _dbContext = dbContext;
        _signer = signer;
    }

    public async Task<List<Ticket>> IssueAsync(
        Guid orderId,
        Guid sessionId,
        List<(Guid SeatId, string SeatLabel, string LegendName, decimal Price)> seats)
    {
        // Idempotency: IssueTicketsConsumer retries up to 5× on transient failures.
        // If tickets were already committed on a prior attempt, return them instead of inserting duplicates.
        var existing = await _dbContext.Tickets
            .Where(t => t.OrderId == orderId)
            .ToListAsync();
        if (existing.Count > 0)
            return existing;

        var tickets = seats.Select(s =>
        {
            var ticket = new Ticket
            {
                OrderId = orderId,
                SessionId = sessionId,
                SeatId = s.SeatId,
                SeatLabel = s.SeatLabel,
                LegendName = s.LegendName,
                Price = s.Price
            };
            // QR payload is HMAC-signed so it can't be forged by guessing GUIDs
            ticket.QRCode = QrCodeGenerator.GenerateBase64Png(_signer.Sign(ticket.Id));
            return ticket;
        }).ToList();

        _dbContext.Tickets.AddRange(tickets);
        await _dbContext.SaveChangesAsync();
        return tickets;
    }

    public async Task<List<Ticket>> GetByOrderAsync(Guid orderId)
    {
        return await _dbContext.Tickets
            .Where(t => t.OrderId == orderId)
            .ToListAsync();
    }

    public async Task<List<EventCheckInItem>> GetCheckedInByEventAsync(Guid eventId)
    {
        var query = from t in _dbContext.Tickets
                    join s in _dbContext.Sessions on t.SessionId equals s.Id
                    where s.EventId == eventId && t.IsCheckedIn
                    orderby t.CheckedInAt descending
                    select new EventCheckInItem(
                        t.Id,
                        t.SeatLabel,
                        t.LegendName,
                        t.Price,
                        s.Name,
                        s.StartTime,
                        t.CheckedInAt!.Value);

        return await query.ToListAsync();
    }

    public async Task<List<OrgCheckInItem>> GetCheckedInByOrgAsync(Guid orgId)
    {
        var query = from t in _dbContext.Tickets
                    join s in _dbContext.Sessions on t.SessionId equals s.Id
                    join e in _dbContext.Events on s.EventId equals e.Id
                    where e.OrganizationId == orgId && t.IsCheckedIn
                    orderby t.CheckedInAt descending
                    select new OrgCheckInItem(
                        t.Id,
                        t.SeatLabel,
                        t.LegendName,
                        t.Price,
                        s.Name,
                        s.StartTime,
                        e.Name,
                        e.Id,
                        t.CheckedInAt!.Value);

        return await query.ToListAsync();
    }

    public async Task<Ticket> CheckInAsync(string signedToken, Guid staffUserId)
    {
        if (!_signer.TryVerify(signedToken, out var ticketId))
            throw new UnauthorizedException("Invalid or tampered ticket QR.");

        var ticket = await _dbContext.Tickets.FindAsync(ticketId)
            ?? throw new NotFoundException($"Ticket not found.");

        if (ticket.IsCheckedIn)
            throw new BusinessException($"Ticket already checked in at {ticket.CheckedInAt:HH:mm dd/MM/yyyy}");

        ticket.IsCheckedIn = true;
        ticket.CheckedInAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return ticket;
    }
}
