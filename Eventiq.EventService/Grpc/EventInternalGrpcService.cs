using Eventiq.Contracts.Grpc;
using Eventiq.EventService.Application.Service.Interface;
using Eventiq.EventService.Domain.Repositories;
using Grpc.Core;

namespace Eventiq.EventService.Grpc;

public class EventInternalGrpcService : EventInternal.EventInternalBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILegendRepository _legendRepository;
    private readonly ITicketService _ticketService;
    private readonly ILogger<EventInternalGrpcService> _logger;

    public EventInternalGrpcService(
        ISessionRepository sessionRepository,
        ILegendRepository legendRepository,
        ITicketService ticketService,
        ILogger<EventInternalGrpcService> logger)
    {
        _sessionRepository = sessionRepository;
        _legendRepository = legendRepository;
        _ticketService = ticketService;
        _logger = logger;
    }

    public override async Task<SessionInfo> GetSession(GetSessionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SessionId, out var sessionId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid session_id"));

        var s = await _sessionRepository.GetSessionInternalAsync(sessionId);
        if (s == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Session {sessionId} not found"));

        return new SessionInfo
        {
            SessionId = s.SessionId.ToString(),
            SessionName = s.SessionName,
            StartTime = s.StartTime.ToString("O"),
            EndTime = s.EndTime.ToString("O"),
            EventId = s.EventId.ToString(),
            EventName = s.EventName,
            OrgId = s.OrgId.ToString()
        };
    }

    public override async Task<GetLegendsResponse> GetLegends(GetLegendsRequest request, ServerCallContext context)
    {
        var ids = request.LegendIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        var response = new GetLegendsResponse();
        if (ids.Count == 0) return response;

        var legends = await _legendRepository.GetByIdsAsync(ids);
        foreach (var l in legends)
        {
            response.Legends.Add(new LegendInfo
            {
                LegendId = l.Id.ToString(),
                Name = l.Name,
                Price = l.Price
            });
        }
        return response;
    }

    public override async Task<IssueTicketsResponse> IssueTickets(IssueTicketsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId) || !Guid.TryParse(request.SessionId, out var sessionId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid order_id or session_id"));

        var seats = request.Seats.Select(s => (
            Guid.Parse(s.SeatId),
            s.SeatLabel,
            s.LegendName,
            (decimal)s.Price
        )).ToList();

        var tickets = await _ticketService.IssueAsync(orderId, sessionId, seats);

        var response = new IssueTicketsResponse { Success = true };
        foreach (var t in tickets)
            response.Tickets.Add(new IssuedTicketInfo
            {
                TicketId = t.Id.ToString(),
                SeatLabel = t.SeatLabel,
                QrCode = t.QRCode
            });
        return response;
    }

    public override async Task<GetTicketsByOrderResponse> GetTicketsByOrder(GetTicketsByOrderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid order_id"));

        var tickets = await _ticketService.GetByOrderAsync(orderId);
        var response = new GetTicketsByOrderResponse();
        foreach (var t in tickets)
            response.Tickets.Add(new TicketDetail
            {
                TicketId = t.Id.ToString(),
                SeatLabel = t.SeatLabel,
                LegendName = t.LegendName,
                Price = (double)t.Price,
                QrCode = t.QRCode,
                IsCheckedIn = t.IsCheckedIn
            });
        return response;
    }
}
