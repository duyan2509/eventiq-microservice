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

    public SeatInternalGrpcService(
        ISeatRepository seatRepository,
        ISeatMapRepository seatMapRepository,
        ISeatReservationService reservation)
    {
        _seatRepository = seatRepository;
        _seatMapRepository = seatMapRepository;
        _reservation = reservation;
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
}
