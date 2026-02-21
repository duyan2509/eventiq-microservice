using AutoMapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Extensions;
using Eventiq.EventService.Guards;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.EventService.Application.Service;

public class SubmissionService : ISubmissionService
{
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _uow;

    public SubmissionService(IMapper mapper, IUnitOfWork uow)
    {
        _mapper = mapper;
        _uow = uow;
    }

    public async Task<PaginatedResult<SubmissionResponse>> GetAllSubmissionByEventIdAsync(Guid userId, Guid eventId)
    {
        var rs = await _uow.Submissions.GetAllSubmissionsByEventIdAsync(eventId);
        var data = rs.Data.Select(lg=>_mapper.Map<SubmissionResponse>(lg));
        return new PaginatedResult<SubmissionResponse>()
        {
            Data = data,
            Total = rs.Total,
            Page = rs.Page,
            Size = rs.Size
        };
    }

    public async Task<SubmissionResponse> SubmitEventAsync(Guid userId, Guid orgId, Guid eventId)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            EventGuards.EnsureStatus(evt, EventStatus.Draft);
            await _uow.Events.SetEventStatusAsync(eventId,EventStatus.Pending);
            var submission = new Submission()
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
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureStatus(evt,EventStatus.Pending);
            // check validate 
            
            //call grpc to org service to get payment infor, if exist: published, else approved
            bool existPayment = true;
            var status = existPayment? EventStatus.Published : EventStatus.Approved;
            var message = !existPayment? "\nPlease provide organization payment information to publish event":"";
            var submission = new Submission()
            {
                EventId = eventId,
                AdminId = userId,
                AdminEmail = adminEmail,
                Message = dto.Message+ message,
                Status = status
            };
            await _uow.Submissions.AddAsync(eventId, submission);
            await _uow.Events.SetEventStatusAsync(eventId,status);

            await _uow.CommitAsync();

            return _mapper.Map<SubmissionResponse>(submission);
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<SubmissionResponse> RejectEventAsync(Guid userId, string adminEmail, Guid eventId, UpdateSubmissioDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            var submission = new Submission()
            {
                EventId = eventId,
                AdminId = userId,
                AdminEmail = adminEmail,
                Message = dto.Message,
                Status = EventStatus.Rejected
            };
            await _uow.Submissions.AddAsync(eventId, submission);
            await _uow.Events.SetEventStatusAsync(eventId,EventStatus.Draft);

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
