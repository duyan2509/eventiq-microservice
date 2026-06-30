using AutoMapper;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;
using NSubstitute;
using Xunit;

namespace Eventiq.EventService.Tests;

public class SessionServiceCreateTests
{
    private readonly IUnitOfWork _uow;
    private readonly IEventRepository _eventRepo;
    private readonly IChartRepository _chartRepo;
    private readonly ISessionRepository _sessionRepo;
    private readonly IMapper _mapper;
    private readonly ISeatServiceClient _seatClient;
    private readonly SessionService _service;

    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid OrgId     = Guid.NewGuid();
    private static readonly Guid EventId   = Guid.NewGuid();
    private static readonly Guid ChartId   = Guid.NewGuid();

    public SessionServiceCreateTests()
    {
        _uow         = Substitute.For<IUnitOfWork>();
        _eventRepo   = Substitute.For<IEventRepository>();
        _chartRepo   = Substitute.For<IChartRepository>();
        _sessionRepo = Substitute.For<ISessionRepository>();

        _uow.Events.Returns(_eventRepo);
        _uow.Charts.Returns(_chartRepo);
        _uow.Sessions.Returns(_sessionRepo);
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitAsync().Returns(Task.CompletedTask);
        _uow.RollbackAsync().Returns(Task.CompletedTask);

        _mapper     = Substitute.For<IMapper>();
        _seatClient = Substitute.For<ISeatServiceClient>();
        _seatClient.InitSessionSeatMapAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(Guid.NewGuid());

        _service = new SessionService(_uow, _mapper, _seatClient);

        SetupHappyPath();
    }

    private Session _session = null!;

    private void SetupHappyPath()
    {
        _eventRepo.GetByIdAsync(EventId)
            .Returns(new EventModel { Id = EventId, OrganizationId = OrgId, Status = EventStatus.Draft });

        _chartRepo.GetChartByIdEventIdAsync(EventId, Arg.Any<Guid?>())
            .Returns(new ChartModel { Id = ChartId, EventId = EventId, Name = "Main" });

        _session = new Session
        {
            Id        = Guid.NewGuid(),
            ChartId   = ChartId,
            EventId   = EventId,
            Name      = "Morning Show",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime   = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _mapper.Map<Session>(Arg.Any<CreateSessionDto>()).Returns(_session);
        _mapper.Map<SessionResponse>(Arg.Any<Session>())
            .Returns(new SessionResponse
            {
                Id        = _session.Id,
                Name      = _session.Name,
                StartTime = _session.StartTime,
                EndTime   = _session.EndTime,
                ChartId   = _session.ChartId
            });

        _sessionRepo.CheckOverlappedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(false);
        _sessionRepo.AddAsync(Arg.Any<Guid>(), Arg.Any<Session>()).Returns(1);
    }

    private CreateSessionDto ValidDto() => new()
    {
        Name      = "Morning Show",
        StartTime = DateTime.UtcNow.AddDays(1),
        EndTime   = DateTime.UtcNow.AddDays(1).AddHours(2),
        ChartId   = ChartId
    };

    // ── 1. InitSessionSeatMapAsync is called with correct session and chart ───
    [Fact]
    public async Task CreateSessionAsync_WhenSessionCreated_CallsInitSeatMap()
    {
        await _service.CreateSessionAsync(UserId, OrgId, EventId, ValidDto());

        await _seatClient.Received(1)
            .InitSessionSeatMapAsync(
                Arg.Any<Guid>(),
                ChartId,
                EventId);
    }

    // ── 2. Session is returned even when seat client returns null ─────────────
    [Fact]
    public async Task CreateSessionAsync_WhenSeatClientReturnsNull_StillReturnsSession()
    {
        _seatClient.InitSessionSeatMapAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((Guid?)null);

        var result = await _service.CreateSessionAsync(UserId, OrgId, EventId, ValidDto());

        Assert.NotNull(result);
    }

    // ── 3. Sessions.AddAsync is always called (session persisted) ────────────
    [Fact]
    public async Task CreateSessionAsync_AlwaysPersistsSession()
    {
        await _service.CreateSessionAsync(UserId, OrgId, EventId, ValidDto());

        await _sessionRepo.Received(1).AddAsync(EventId, Arg.Any<Session>());
        await _uow.Received(1).CommitAsync();
    }

    // ── 4. InitSeatMap called AFTER CommitAsync (session exists first) ────────
    [Fact]
    public async Task CreateSessionAsync_InitSeatMapCalledAfterCommit()
    {
        var callOrder = new List<string>();
        _uow.CommitAsync().Returns(_ => { callOrder.Add("commit"); return Task.CompletedTask; });
        _seatClient.InitSessionSeatMapAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(_ => { callOrder.Add("initSeat"); return Task.FromResult<Guid?>(Guid.NewGuid()); });

        await _service.CreateSessionAsync(UserId, OrgId, EventId, ValidDto());

        Assert.Equal(new[] { "commit", "initSeat" }, callOrder);
    }

    // ── 5. Throws NotFoundException when event not found ─────────────────────
    [Fact]
    public async Task CreateSessionAsync_WhenEventNotFound_Throws()
    {
        _eventRepo.GetByIdAsync(EventId).Returns((EventModel?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateSessionAsync(UserId, OrgId, EventId, ValidDto()));
    }

    // ── 6. Throws ForbiddenException when org doesn't own event ──────────────
    [Fact]
    public async Task CreateSessionAsync_WhenWrongOrg_ThrowsForbidden()
    {
        _eventRepo.GetByIdAsync(EventId)
            .Returns(new EventModel { Id = EventId, OrganizationId = Guid.NewGuid() });

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.CreateSessionAsync(UserId, OrgId, EventId, ValidDto()));
    }

    // ── 7. Throws when sessions overlap ──────────────────────────────────────
    [Fact]
    public async Task CreateSessionAsync_WhenOverlap_ThrowsBusinessException()
    {
        _sessionRepo.CheckOverlappedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(true);

        await Assert.ThrowsAsync<BusinessException>(
            () => _service.CreateSessionAsync(UserId, OrgId, EventId, ValidDto()));
    }
}
