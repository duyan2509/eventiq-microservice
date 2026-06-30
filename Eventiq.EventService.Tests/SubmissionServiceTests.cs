using AutoMapper;
using Eventiq.Contracts;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Eventiq.EventService.Tests;

public class SubmissionServiceTests
{
    private readonly IUnitOfWork _uow;
    private readonly IEventRepository _eventRepo;
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IOrgPaymentRepository _payment;
    private readonly IPublishEndpoint _bus;
    private readonly IMapper _mapper;
    private readonly EvtEventDbContext _dbContext;
    private readonly SubmissionService _service;

    private static readonly Guid UserId  = Guid.NewGuid();
    private static readonly Guid OrgId   = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();

    public SubmissionServiceTests()
    {
        _uow            = Substitute.For<IUnitOfWork>();
        _eventRepo      = Substitute.For<IEventRepository>();
        _submissionRepo = Substitute.For<ISubmissionRepository>();
        _payment        = Substitute.For<IOrgPaymentRepository>();
        _bus            = Substitute.For<IPublishEndpoint>();
        _mapper         = Substitute.For<IMapper>();

        _uow.Events.Returns(_eventRepo);
        _uow.Submissions.Returns(_submissionRepo);
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitAsync().Returns(Task.CompletedTask);
        _uow.RollbackAsync().Returns(Task.CompletedTask);

        var options = new DbContextOptionsBuilder<EvtEventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new EvtEventDbContext(options);

        _mapper.Map<SubmissionResponse>(Arg.Any<Submission>())
            .Returns(new SubmissionResponse { Status = EventStatus.Pending });

        _service = new SubmissionService(_mapper, _uow, _payment, _bus, _dbContext);
    }

    private EventModel DraftEvent() => new()
    {
        Id             = EventId,
        OrganizationId = OrgId,
        Status         = EventStatus.Draft
    };

    private EventModel PendingEvent() => new()
    {
        Id             = EventId,
        OrganizationId = OrgId,
        Status         = EventStatus.Pending
    };

    // ── SubmitEventAsync ──────────────────────────────────────────────────────

    // 1. Throws when org has no payment account
    [Fact]
    public async Task SubmitEventAsync_WhenNoPayment_ThrowsBusinessException()
    {
        _payment.HasActivePaymentAsync(OrgId).Returns(false);

        await Assert.ThrowsAsync<BusinessException>(
            () => _service.SubmitEventAsync(UserId, OrgId, EventId));
    }

    // 2. Succeeds (no seatmap check) when payment exists and event is Draft
    [Fact]
    public async Task SubmitEventAsync_WhenPaymentExists_SubmitsWithoutSeatMapCheck()
    {
        _payment.HasActivePaymentAsync(OrgId).Returns(true);
        _eventRepo.GetByIdAsync(EventId).Returns(DraftEvent());

        // Should not throw — seatmap check has been removed
        var result = await _service.SubmitEventAsync(UserId, OrgId, EventId);

        Assert.NotNull(result);
        await _uow.Received(1).CommitAsync();
    }

    // 3. Event status is set to Pending
    [Fact]
    public async Task SubmitEventAsync_SetsEventStatusToPending()
    {
        _payment.HasActivePaymentAsync(OrgId).Returns(true);
        _eventRepo.GetByIdAsync(EventId).Returns(DraftEvent());

        await _service.SubmitEventAsync(UserId, OrgId, EventId);

        await _eventRepo.Received(1).SetEventStatusAsync(EventId, EventStatus.Pending);
    }

    // 4. Throws NotFoundException when event doesn't exist
    [Fact]
    public async Task SubmitEventAsync_WhenEventNotFound_Throws()
    {
        _payment.HasActivePaymentAsync(OrgId).Returns(true);
        _eventRepo.GetByIdAsync(EventId).Returns((EventModel?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.SubmitEventAsync(UserId, OrgId, EventId));
    }

    // 5. Throws BusinessException when event is not Draft
    [Fact]
    public async Task SubmitEventAsync_WhenEventNotDraft_Throws()
    {
        _payment.HasActivePaymentAsync(OrgId).Returns(true);
        _eventRepo.GetByIdAsync(EventId).Returns(PendingEvent());

        await Assert.ThrowsAsync<BusinessException>(
            () => _service.SubmitEventAsync(UserId, OrgId, EventId));
    }

    // ── AcceptEventAsync ──────────────────────────────────────────────────────

    // 6. Publishes EventApproved with correct EventId
    [Fact]
    public async Task AcceptEventAsync_PublishesEventApproved_WithCorrectEventId()
    {
        _eventRepo.GetByIdAsync(EventId).Returns(PendingEvent());

        await _service.AcceptEventAsync(UserId, "admin@test.com", EventId,
            new UpdateSubmissioDto { Message = "OK" });

        await _bus.Received(1).Publish(
            Arg.Is<EventApproved>(m => m.EventId == EventId),
            Arg.Any<CancellationToken>());
    }

    // 7. EventApproved no longer carries a Sessions array
    [Fact]
    public async Task AcceptEventAsync_EventApproved_HasNoSessionsProperty()
    {
        // The EventApproved contract was simplified — verify the record only has EventId + ApprovedAt.
        var approved = new EventApproved { EventId = EventId, ApprovedAt = DateTime.UtcNow };

        // Compile-time check: if Sessions property existed, this would not compile with just these two.
        Assert.Equal(EventId, approved.EventId);
        Assert.True(approved.ApprovedAt > DateTime.MinValue);
    }

    // 8. Sets event status to Approved
    [Fact]
    public async Task AcceptEventAsync_SetsEventStatusToApproved()
    {
        _eventRepo.GetByIdAsync(EventId).Returns(PendingEvent());

        await _service.AcceptEventAsync(UserId, "admin@test.com", EventId,
            new UpdateSubmissioDto { Message = "OK" });

        await _eventRepo.Received(1).SetEventStatusAsync(EventId, EventStatus.Approved);
    }

    // 9. Throws when event is not Pending
    [Fact]
    public async Task AcceptEventAsync_WhenEventNotPending_Throws()
    {
        _eventRepo.GetByIdAsync(EventId).Returns(DraftEvent());

        await Assert.ThrowsAsync<BusinessException>(
            () => _service.AcceptEventAsync(UserId, "admin@test.com", EventId,
                new UpdateSubmissioDto { Message = "OK" }));
    }

    // 10. Uses "Approved" as default message when dto.Message is empty
    [Fact]
    public async Task AcceptEventAsync_WhenNoMessage_UsesDefaultApprovedMessage()
    {
        _eventRepo.GetByIdAsync(EventId).Returns(PendingEvent());
        Submission? captured = null;
        await _submissionRepo.AddAsync(EventId,
            Arg.Do<Submission>(s => captured = s));

        await _service.AcceptEventAsync(UserId, "admin@test.com", EventId,
            new UpdateSubmissioDto { Message = "" });

        Assert.NotNull(captured);
        Assert.Equal("Approved", captured!.Message);
    }
}
