using AutoMapper;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Service.Implement;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
using Eventiq.SeatService.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eventiq.SeatService.Tests;

public class SeatDesignConflictTests
{
    private readonly IUnitOfWork _uow;
    private readonly ISeatMapRepository _seatMaps;
    private readonly ISeatRepository _seats;
    private readonly IMapper _mapper;
    private readonly SeatDesignService _svc;

    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid SeatMapId = Guid.NewGuid();

    public SeatDesignConflictTests()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _seatMaps = Substitute.For<ISeatMapRepository>();
        _seats = Substitute.For<ISeatRepository>();
        _uow.SeatMaps.Returns(_seatMaps);
        _uow.Seats.Returns(_seats);
        _uow.SaveChangesAsync().Returns(1);

        _mapper = Substitute.For<IMapper>();
        _mapper.Map<List<SeatResponse>>(Arg.Any<List<Seat>>()).Returns(callInfo =>
            callInfo.Arg<List<Seat>>().Select(s => new SeatResponse
            {
                Id = s.Id, SeatMapId = s.SeatMapId, Label = s.Label,
                GeometryVersion = s.GeometryVersion, StyleVersion = s.StyleVersion
            }).ToList());

        _svc = new SeatDesignService(_uow, _mapper, NullLogger<SeatDesignService>.Instance);
    }

    private SeatMap MakeSeatMap() => new()
    {
        Id = SeatMapId,
        OrganizationId = OrgId,
        Status = SeatMapStatus.Draft,
        Version = 1,
    };

    private Seat MakeSeat(Guid id, int geometryVersion = 1, int styleVersion = 1) => new()
    {
        Id = id,
        SeatMapId = SeatMapId,
        Label = "S1",
        SeatNumber = 1,
        Status = SeatStatus.Available,
        SeatType = 1,
        GeometryVersion = geometryVersion,
        StyleVersion = styleVersion,
    };

    // =========================================================
    // Test 1: Stale geometry version → seat goes to Conflicted
    // =========================================================
    [Fact]
    public async Task BatchUpdateSeats_StaleGeometryVersion_ReturnsConflict()
    {
        var seatId = Guid.NewGuid();
        var seat = MakeSeat(seatId, geometryVersion: 3);

        _seatMaps.GetByIdAsync(SeatMapId).Returns(MakeSeatMap());
        _seats.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat]);

        var dto = new BatchUpdateSeatsDto
        {
            Seats = [new UpdateSeatDto { SeatId = seatId, Position = "{\"x\":100,\"y\":200}", ExpectedGeometryVersion = 1 }]
        };

        var result = await _svc.BatchUpdateSeatsAsync(SeatMapId, OrgId, dto);

        Assert.Empty(result.Updated);
        Assert.Single(result.Conflicted);
        Assert.Equal(seatId, result.Conflicted[0].SeatId);
        Assert.Equal(3, result.Conflicted[0].CurrentVersion);
        Assert.Equal("geometry", result.Conflicted[0].PropertyGroup);

        await _seats.DidNotReceive().UpdateRangeAsync(Arg.Any<IEnumerable<Seat>>());
    }

    // =========================================================
    // Test 2: Matching geometry version → seat updated
    // =========================================================
    [Fact]
    public async Task BatchUpdateSeats_MatchingGeometryVersion_ReturnsUpdated()
    {
        var seatId = Guid.NewGuid();
        var seat = MakeSeat(seatId, geometryVersion: 2);

        _seatMaps.GetByIdAsync(SeatMapId).Returns(MakeSeatMap());
        _seats.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat]);

        var dto = new BatchUpdateSeatsDto
        {
            Seats = [new UpdateSeatDto { SeatId = seatId, Position = "{\"x\":100,\"y\":200}", ExpectedGeometryVersion = 2 }]
        };

        var result = await _svc.BatchUpdateSeatsAsync(SeatMapId, OrgId, dto);

        Assert.Single(result.Updated);
        Assert.Empty(result.Conflicted);
        Assert.Equal(3, seat.GeometryVersion);  // incremented
        await _seats.Received(1).UpdateRangeAsync(Arg.Any<IEnumerable<Seat>>());
    }

    // =========================================================
    // Test 3: Two seats, only the stale one conflicts
    // =========================================================
    [Fact]
    public async Task BatchUpdateSeats_MixedVersions_PartialSuccess()
    {
        var seatAId = Guid.NewGuid();
        var seatBId = Guid.NewGuid();
        var seatA = MakeSeat(seatAId, geometryVersion: 1);
        var seatB = MakeSeat(seatBId, geometryVersion: 5);  // stale — client has v3

        _seatMaps.GetByIdAsync(SeatMapId).Returns(MakeSeatMap());
        _seats.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seatA, seatB]);

        var dto = new BatchUpdateSeatsDto
        {
            Seats =
            [
                new UpdateSeatDto { SeatId = seatAId, Position = "{\"x\":10,\"y\":20}", ExpectedGeometryVersion = 1 },
                new UpdateSeatDto { SeatId = seatBId, Position = "{\"x\":30,\"y\":40}", ExpectedGeometryVersion = 3 },
            ]
        };

        var result = await _svc.BatchUpdateSeatsAsync(SeatMapId, OrgId, dto);

        Assert.Single(result.Updated);
        Assert.Single(result.Conflicted);
        Assert.Equal(seatAId, result.Updated[0].Id);
        Assert.Equal(seatBId, result.Conflicted[0].SeatId);
        Assert.Equal(5, result.Conflicted[0].CurrentVersion);
    }

    // =========================================================
    // Test 4: UpdateSeats (geometry) + SetSeatLegend (style) on
    // the same seat at the same version — both succeed because
    // they check different property-version fields.
    // =========================================================
    [Fact]
    public async Task BatchUpdateSeats_ThenSetLegend_SameSeat_BothSucceed()
    {
        var seatId = Guid.NewGuid();
        var seat = MakeSeat(seatId, geometryVersion: 1, styleVersion: 1);

        _seatMaps.GetByIdAsync(SeatMapId).Returns(MakeSeatMap());
        _seats.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat]);

        // Simulate User A: move seat (geometry)
        var updateDto = new BatchUpdateSeatsDto
        {
            Seats = [new UpdateSeatDto { SeatId = seatId, Position = "{\"x\":50,\"y\":50}", ExpectedGeometryVersion = 1 }]
        };
        var updateResult = await _svc.BatchUpdateSeatsAsync(SeatMapId, OrgId, updateDto);

        // GeometryVersion is now 2, StyleVersion is still 1
        Assert.Single(updateResult.Updated);
        Assert.Equal(2, seat.GeometryVersion);
        Assert.Equal(1, seat.StyleVersion);

        // Simulate User B: change legend (style) — still has styleVersion=1
        var legendResult = await _svc.SetSeatLegendAsync(
            SeatMapId, OrgId,
            [seatId], Guid.NewGuid(),
            expectedStyleVersions: new Dictionary<Guid, int> { [seatId] = 1 });

        Assert.Single(legendResult.Updated);
        Assert.Empty(legendResult.Conflicted);
        Assert.Equal(2, seat.StyleVersion);  // incremented
    }

    // =========================================================
    // Test 5: SetSeatLegend with stale style version → conflicts
    // =========================================================
    [Fact]
    public async Task SetSeatLegend_StaleStyleVersion_ReturnsConflict()
    {
        var seatId = Guid.NewGuid();
        var seat = MakeSeat(seatId, geometryVersion: 1, styleVersion: 4);

        _seatMaps.GetByIdAsync(SeatMapId).Returns(MakeSeatMap());
        _seats.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat]);

        var result = await _svc.SetSeatLegendAsync(
            SeatMapId, OrgId,
            [seatId], Guid.NewGuid(),
            expectedStyleVersions: new Dictionary<Guid, int> { [seatId] = 2 });

        Assert.Empty(result.Updated);
        Assert.Single(result.Conflicted);
        Assert.Equal("style", result.Conflicted[0].PropertyGroup);
        Assert.Equal(4, result.Conflicted[0].CurrentVersion);
    }
}
