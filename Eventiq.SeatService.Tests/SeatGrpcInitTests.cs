using Eventiq.Contracts.Grpc;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Repositories;
using Eventiq.SeatService.Grpc;
using Grpc.Core;
using NSubstitute;

namespace Eventiq.SeatService.Tests;

public class SeatGrpcInitTests
{
    private readonly ISeatMapService _seatMapService;
    private readonly SeatInternalGrpcService _grpc;

    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ChartId   = Guid.NewGuid();
    private static readonly Guid EventId   = Guid.NewGuid();

    public SeatGrpcInitTests()
    {
        _seatMapService = Substitute.For<ISeatMapService>();

        _grpc = new SeatInternalGrpcService(
            Substitute.For<ISeatRepository>(),
            Substitute.For<ISeatMapRepository>(),
            Substitute.For<ISeatReservationService>(),
            _seatMapService);
    }

    private static InitSessionSeatMapRequest ValidRequest() => new()
    {
        SessionId = SessionId.ToString(),
        ChartId   = ChartId.ToString(),
        EventId   = EventId.ToString()
    };

    // ── 1. Clone succeeds → Success=true and SeatMapId returned ──────────────
    [Fact]
    public async Task InitSessionSeatMap_WhenCloneSucceeds_ReturnsSuccessTrue()
    {
        var seatMapId = Guid.NewGuid();
        _seatMapService
            .CloneForSessionAsync(SessionId, ChartId, EventId)
            .Returns(new SeatMapResponse { Id = seatMapId, SessionId = SessionId });

        var response = await _grpc.InitSessionSeatMap(ValidRequest(), null!);

        Assert.True(response.Success);
        Assert.Equal(seatMapId.ToString(), response.SeatMapId);
    }

    // ── 2. Clone returns null (no template) → Success=false ──────────────────
    [Fact]
    public async Task InitSessionSeatMap_WhenNoTemplate_ReturnsSuccessFalse()
    {
        _seatMapService
            .CloneForSessionAsync(SessionId, ChartId, EventId)
            .Returns((SeatMapResponse?)null);

        var response = await _grpc.InitSessionSeatMap(ValidRequest(), null!);

        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.SeatMapId);
    }

    // ── 3. Invalid SessionId → RpcException InvalidArgument ──────────────────
    [Fact]
    public async Task InitSessionSeatMap_WhenInvalidSessionId_ThrowsRpcInvalidArgument()
    {
        var req = new InitSessionSeatMapRequest
        {
            SessionId = "not-a-guid",
            ChartId   = ChartId.ToString(),
            EventId   = EventId.ToString()
        };

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _grpc.InitSessionSeatMap(req, null!));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // ── 4. Invalid ChartId → RpcException InvalidArgument ────────────────────
    [Fact]
    public async Task InitSessionSeatMap_WhenInvalidChartId_ThrowsRpcInvalidArgument()
    {
        var req = new InitSessionSeatMapRequest
        {
            SessionId = SessionId.ToString(),
            ChartId   = "bad",
            EventId   = EventId.ToString()
        };

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _grpc.InitSessionSeatMap(req, null!));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // ── 5. Invalid EventId → RpcException InvalidArgument ────────────────────
    [Fact]
    public async Task InitSessionSeatMap_WhenInvalidEventId_ThrowsRpcInvalidArgument()
    {
        var req = new InitSessionSeatMapRequest
        {
            SessionId = SessionId.ToString(),
            ChartId   = ChartId.ToString(),
            EventId   = "bad"
        };

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _grpc.InitSessionSeatMap(req, null!));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // ── 6. CloneForSessionAsync called with correct args ─────────────────────
    [Fact]
    public async Task InitSessionSeatMap_CallsCloneWithCorrectIds()
    {
        _seatMapService
            .CloneForSessionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((SeatMapResponse?)null);

        await _grpc.InitSessionSeatMap(ValidRequest(), null!);

        await _seatMapService.Received(1)
            .CloneForSessionAsync(SessionId, ChartId, EventId);
    }
}
