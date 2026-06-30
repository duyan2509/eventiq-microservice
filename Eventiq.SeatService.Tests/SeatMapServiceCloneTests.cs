using AutoMapper;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Service.Implement;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
using Eventiq.SeatService.Domain.Repositories;
using MassTransit;
using NSubstitute;

namespace Eventiq.SeatService.Tests;

public class SeatMapServiceCloneTests
{
    private readonly IUnitOfWork _uow;
    private readonly ISeatMapRepository _seatMapRepo;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _bus;
    private readonly SeatMapService _service;

    private static readonly Guid OrgId     = Guid.NewGuid();
    private static readonly Guid EventId   = Guid.NewGuid();
    private static readonly Guid ChartId   = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    public SeatMapServiceCloneTests()
    {
        _uow         = Substitute.For<IUnitOfWork>();
        _seatMapRepo = Substitute.For<ISeatMapRepository>();
        _uow.SeatMaps.Returns(_seatMapRepo);
        _uow.SaveChangesAsync().Returns(1);

        _mapper = Substitute.For<IMapper>();
        _bus    = Substitute.For<IPublishEndpoint>();

        _service = new SeatMapService(_uow, _mapper, _bus);
    }

    private SeatMap MakeTemplate(int seatCount = 3, int objCount = 1)
    {
        var t = new SeatMap
        {
            Id             = Guid.NewGuid(),
            ChartId        = ChartId,
            EventId        = EventId,
            OrganizationId = OrgId,
            Name           = "Main Stage",
            Status         = SeatMapStatus.Draft,
            CanvasSettings = "{}"
        };
        for (var i = 0; i < seatCount; i++)
            t.Seats.Add(new Seat { Id = Guid.NewGuid(), SeatMapId = t.Id, Label = $"A{i + 1}",
                Status = SeatStatus.Available, SeatNumber = i + 1 });
        for (var i = 0; i < objCount; i++)
            t.Objects.Add(new SeatObject { Id = Guid.NewGuid(), SeatMapId = t.Id, Label = $"Obj{i}" });
        return t;
    }

    private SeatMapResponse FakeResponse(Guid id) => new() { Id = id, SessionId = SessionId };

    // ── 1. Existing clone is returned without creating a new one ──────────────
    [Fact]
    public async Task CloneForSessionAsync_WhenCloneAlreadyExists_ReturnsExistingWithoutSave()
    {
        var existing = new SeatMap { Id = Guid.NewGuid(), SessionId = SessionId };
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns(existing);
        _mapper.Map<SeatMapResponse>(existing).Returns(FakeResponse(existing.Id));

        var result = await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result!.Id);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── 2. Returns null when no template exists for chart or event ────────────
    [Fact]
    public async Task CloneForSessionAsync_WhenNoTemplate_ReturnsNull()
    {
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns((SeatMap?)null);
        _seatMapRepo.GetByEventIdAsync(EventId).Returns(new List<SeatMap>());

        var result = await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.Null(result);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── 3. Template found by ChartId → clone is saved ────────────────────────
    [Fact]
    public async Task CloneForSessionAsync_WhenTemplateFoundByChartId_SavesClone()
    {
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns(MakeTemplate());
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        var result = await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.NotNull(result);
        await _seatMapRepo.Received(1).AddAsync(Arg.Any<SeatMap>());
        await _uow.Received(1).SaveChangesAsync();
    }

    // ── 4. Falls back to event-level template when ChartId has no match ───────
    [Fact]
    public async Task CloneForSessionAsync_WhenNoChartMatch_FallsBackToEventTemplate()
    {
        var template   = MakeTemplate();
        var otherChart = Guid.NewGuid();
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns((SeatMap?)null);
        _seatMapRepo.GetByEventIdAsync(EventId).Returns(new List<SeatMap>
        {
            new() { Id = template.Id, EventId = EventId, ChartId = otherChart, SessionId = null }
        });
        _seatMapRepo.GetByIdWithDetailsAsync(template.Id).Returns(template);
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        var result = await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.NotNull(result);
        await _uow.Received(1).SaveChangesAsync();
    }

    // ── 5. Cloned seats all start as Available ────────────────────────────────
    [Fact]
    public async Task CloneForSessionAsync_Clone_AllSeatsAreAvailable()
    {
        var template = MakeTemplate(seatCount: 4);
        // Poison one seat status in template to confirm clone resets it
        template.Seats.First().Status = SeatStatus.Sold;

        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns(template);

        SeatMap? captured = null;
        await _seatMapRepo.AddAsync(Arg.Do<SeatMap>(m => captured = m));
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.NotNull(captured);
        Assert.All(captured!.Seats, s => Assert.Equal(SeatStatus.Available, s.Status));
    }

    // ── 6. Clone has Published status ────────────────────────────────────────
    [Fact]
    public async Task CloneForSessionAsync_Clone_StatusIsPublished()
    {
        var template = MakeTemplate();
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns(template);

        SeatMap? captured = null;
        await _seatMapRepo.AddAsync(Arg.Do<SeatMap>(m => captured = m));
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.Equal(SeatMapStatus.Published, captured!.Status);
    }

    // ── 7. Clone carries the requested SessionId ─────────────────────────────
    [Fact]
    public async Task CloneForSessionAsync_Clone_HasCorrectSessionId()
    {
        var template = MakeTemplate();
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns(template);

        SeatMap? captured = null;
        await _seatMapRepo.AddAsync(Arg.Do<SeatMap>(m => captured = m));
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.Equal(SessionId, captured!.SessionId);
    }

    // ── 8. Seat count in clone matches the template ───────────────────────────
    [Fact]
    public async Task CloneForSessionAsync_Clone_SeatCountMatchesTemplate()
    {
        const int expectedSeats = 5;
        var template = MakeTemplate(seatCount: expectedSeats);
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns(template);

        SeatMap? captured = null;
        await _seatMapRepo.AddAsync(Arg.Do<SeatMap>(m => captured = m));
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.Equal(expectedSeats, captured!.Seats.Count);
    }

    // ── 9. Objects are copied from template ───────────────────────────────────
    [Fact]
    public async Task CloneForSessionAsync_Clone_ObjectsAreCopied()
    {
        const int expectedObjects = 2;
        var template = MakeTemplate(seatCount: 1, objCount: expectedObjects);
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns(template);

        SeatMap? captured = null;
        await _seatMapRepo.AddAsync(Arg.Do<SeatMap>(m => captured = m));
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.Equal(expectedObjects, captured!.Objects.Count);
    }

    // ── 10. Clone gets a new ID (not the template's ID) ───────────────────────
    [Fact]
    public async Task CloneForSessionAsync_Clone_GetsNewId()
    {
        var template = MakeTemplate();
        _seatMapRepo.GetBySessionIdAsync(SessionId).Returns((SeatMap?)null);
        _seatMapRepo.GetTemplateByChartIdWithDetailsAsync(ChartId).Returns(template);

        SeatMap? captured = null;
        await _seatMapRepo.AddAsync(Arg.Do<SeatMap>(m => captured = m));
        _mapper.Map<SeatMapResponse>(Arg.Any<SeatMap>()).Returns(c => FakeResponse(((SeatMap)c[0]).Id));

        await _service.CloneForSessionAsync(SessionId, ChartId, EventId);

        Assert.NotEqual(template.Id, captured!.Id);
    }
}
