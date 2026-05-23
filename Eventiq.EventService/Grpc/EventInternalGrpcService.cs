using Eventiq.Contracts.Grpc;
using Eventiq.EventService.Domain.Repositories;
using Grpc.Core;

namespace Eventiq.EventService.Grpc;

public class EventInternalGrpcService : EventInternal.EventInternalBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILegendRepository _legendRepository;
    private readonly ILogger<EventInternalGrpcService> _logger;

    public EventInternalGrpcService(
        ISessionRepository sessionRepository,
        ILegendRepository legendRepository,
        ILogger<EventInternalGrpcService> logger)
    {
        _sessionRepository = sessionRepository;
        _legendRepository = legendRepository;
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
}
