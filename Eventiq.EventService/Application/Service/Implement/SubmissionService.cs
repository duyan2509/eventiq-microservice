using AutoMapper;
using Eventiq.Contracts;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Extensions;
using Eventiq.EventService.Guards;
using MassTransit;

namespace Eventiq.EventService.Application.Service;

public class SubmissionService : ISubmissionService
{
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _uow;
    private readonly IOrgPaymentRepository _orgPayment;
    private readonly ISeatServiceClient _seatServiceClient;
    private readonly IPublishEndpoint _publishEndpoint;

    public SubmissionService(
        IMapper mapper,
        IUnitOfWork uow,
        IOrgPaymentRepository orgPayment,
        ISeatServiceClient seatServiceClient,
        IPublishEndpoint publishEndpoint)
    {
        _mapper = mapper;
        _uow = uow;
        _orgPayment = orgPayment;
        _seatServiceClient = seatServiceClient;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<PaginatedResult<SubmissionResponse>> GetAllSubmissionByEventIdAsync(Guid userId, Guid eventId, int page = 1, int size = 20)
    {
        var rs = await _uow.Submissions.GetAllSubmissionsByEventIdAsync(eventId, page, size);
        var data = rs.Data.Select(lg => _mapper.Map<SubmissionResponse>(lg));
        return new PaginatedResult<SubmissionResponse>
        {
            Data = data,
            Total = rs.Total,
            Page = rs.Page,
            Size = rs.Size
        };
    }

    public async Task<SubmissionResponse> SubmitEventAsync(Guid userId, Guid orgId, Guid eventId)
    {
        var hasPayment = await _orgPayment.HasActivePaymentAsync(orgId);
        if (!hasPayment)
            throw new BusinessException(
                "Organization has no active payment account. Please connect Stripe before submitting the event.");

        var hasSeatMap = await _seatServiceClient.HasSeatMapDesignAsync(eventId);
        if (!hasSeatMap)
            throw new BusinessException(
                "Event has no seat map design. Please design a seat map before submitting.");

        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            EventGuards.EnsureStatus(evt, EventStatus.Draft);
            await _uow.Events.SetEventStatusAsync(eventId, EventStatus.Pending);
            var submission = new Submission
            {
                EventId = eventId,
                AdminId = Guid.Empty,
                AdminEmail = string.Empty,
                Message = "Request Review",
                Status = EventStatus.Pending
            };
            await _uow.Submissions.AddAsync(eventId, submission);
            await _uow.CommitAsync();
            return _mapper.Map<SubmissionResponse>(submission);
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<SubmissionResponse> AcceptEventAsync(Guid userId, string adminEmail, Guid eventId, UpdateSubmissioDto dto)
    {
        // Read sessions before opening the write transaction
        var sessions = await _uow.Sessions.GetAllByEventIdAsync(eventId);

        SubmissionResponse result;
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureStatus(evt, EventStatus.Pending);

            var submission = new Submission
            {
                EventId = eventId,
                AdminId = userId,
                AdminEmail = adminEmail,
                Message = dto.Message,
                Status = EventStatus.Approved
            };
            await _uow.Submissions.AddAsync(eventId, submission);
            await _uow.Events.SetEventStatusAsync(eventId, EventStatus.Approved);
            await _uow.CommitAsync();
            result = _mapper.Map<SubmissionResponse>(submission);
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }

        await _publishEndpoint.Publish(new EventApproved
        {
            EventId = eventId,
            Sessions = sessions
                .Select(s => new SessionChartPair(s.Id, s.ChartId))
                .ToArray(),
            ApprovedAt = DateTime.UtcNow
        });

        return result;
    }

    public async Task<SubmissionResponse> RejectEventAsync(Guid userId, string adminEmail, Guid eventId, UpdateSubmissioDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            var submission = new Submission
            {
                EventId = eventId,
                AdminId = userId,
                AdminEmail = adminEmail,
                Message = dto.Message,
                Status = EventStatus.Rejected
            };
            await _uow.Submissions.AddAsync(eventId, submission);
            await _uow.Events.SetEventStatusAsync(eventId, EventStatus.Draft);
            await _uow.CommitAsync();
            return _mapper.Map<SubmissionResponse>(submission);
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<SubmissionResponse> CancelEventAsync(Guid userId, string adminEmail, Guid eventId, UpdateSubmissioDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureStatus(evt, EventStatus.Pending);
            var submission = new Submission
            {
                EventId = eventId,
                AdminId = userId,
                AdminEmail = adminEmail,
                Message = dto.Message,
                Status = EventStatus.Cancelled
            };
            await _uow.Submissions.AddAsync(eventId, submission);
            await _uow.Events.SetEventStatusAsync(eventId, EventStatus.Draft);
            await _uow.CommitAsync();
            return _mapper.Map<SubmissionResponse>(submission);
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }
}
