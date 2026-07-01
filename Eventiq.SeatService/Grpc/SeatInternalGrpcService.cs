using Eventiq.Contracts.Grpc;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Enum;
using Eventiq.SeatService.Domain.Repositories;
using Grpc.Core;

namespace Eventiq.SeatService.Grpc;

public class SeatInternalGrpcService : SeatInternal.SeatInternalBase
{
    private readonly ISeatRepository _seatRepository;
    private readonly ISeatMapRepository _seatMapRepository;
    private readonly ISeatReservationService _reservation;
    private readonly ISeatMapService _seatMapService;

    public SeatInternalGrpcService(
        ISeatRepository seatRepository,
        ISeatMapRepository seatMapRepository,
        ISeatReservationService reservation,
        ISeatMapService seatMapService)
    {
        _seatRepository = seatRepository;
        _seatMapRepository = seatMapRepository;
        _reservation = reservation;
        _seatMapService = seatMapService;
    }

    public override async Task<GetSeatsResponse> GetSeats(GetSeatsRequest request, ServerCallContext context)
    {
        var ids = request.SeatIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        if (ids.Count == 0)
            return new GetSeatsResponse();

        var seats = await _seatRepository.GetByIdsAsync(ids);

        var response = new GetSeatsResponse();
        foreach (var s in seats)
        {
            response.Seats.Add(new SeatInfo
            {
                SeatId = s.Id.ToString(),
                SeatMapId = s.SeatMapId.ToString(),
                Label = s.Label,
                Status = s.Status.ToString(),
                LegendId = s.LegendId?.ToString() ?? string.Empty,
                HeldBy = s.HeldBy?.ToString() ?? string.Empty
            });
        }
        return response;
    }

    public override async Task<MarkSoldResponse> MarkSold(MarkSoldRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user_id"));

        var ids = request.SeatIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        var result = await _reservation.MarkSoldAsync(ids, userId);
        return new MarkSoldResponse
        {
            Success = result.Success,
            Message = result.Error ?? string.Empty
        };
    }

    public override async Task<CheckSeatMapPublishedResponse> CheckSeatMapPublished(
        CheckSeatMapPublishedRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EventId, out var eventId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid event_id"));

        var maps = await _seatMapRepository.GetByEventIdAsync(eventId);
        var published = maps.FirstOrDefault(m => m.SessionId == null && m.Status == SeatMapStatus.Published);
        return new CheckSeatMapPublishedResponse
        {
            IsPublished = published != null,
            SeatMapId = published?.Id.ToString() ?? string.Empty
        };
    }

    public override async Task<CheckSeatMapDesignResponse> CheckSeatMapDesign(
        CheckSeatMapDesignRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EventId, out var eventId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid event_id"));

        var hasDesign = await _seatMapRepository.HasTemplateForEventAsync(eventId);
        return new CheckSeatMapDesignResponse { HasDesign = hasDesign };
    }

    public override async Task<InitSessionSeatMapResponse> InitSessionSeatMap(
        InitSessionSeatMapRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SessionId, out var sessionId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid session_id"));
        if (!Guid.TryParse(request.ChartId, out var chartId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid chart_id"));
        if (!Guid.TryParse(request.EventId, out var eventId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid event_id"));

        var result = await _seatMapService.CloneForSessionAsync(sessionId, chartId, eventId);
        return new InitSessionSeatMapResponse
        {
            Success = result != null,
            SeatMapId = result?.Id.ToString() ?? string.Empty
        };
    }

    public override async Task<IsLegendUsedInTemplateResponse> IsLegendUsedInTemplate(
        IsLegendUsedInTemplateRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.LegendId, out var legendId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid legend_id"));

        var inUse = await _seatRepository.IsLegendUsedInTemplateAsync(legendId);
        return new IsLegendUsedInTemplateResponse { InUse = inUse };
    }

    public override async Task<CheckSeatMapForChartResponse> CheckSeatMapForChart(
        CheckSeatMapForChartRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ChartId, out var chartId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid chart_id"));

        var exists = await _seatMapRepository.GetByChartIdAsync(chartId) != null;
        return new CheckSeatMapForChartResponse { HasSeatMap = exists };
    }

    public override async Task<ExtendHoldResponse> ExtendHold(ExtendHoldRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user_id"));

        var ids = request.SeatIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        var result = await _reservation.ExtendHoldAsync(ids, userId, TimeSpan.FromSeconds(request.DurationSeconds));
        return new ExtendHoldResponse
        {
            Success = result.Success,
            Message = result.Error ?? string.Empty,
            HeldUntil = result.HeldUntil?.ToString("O") ?? string.Empty
        };
    }
}
