using AutoMapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Extensions;
using Eventiq.EventService.Guards;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.EventService.Application.Service;

public class SessionService : ISessionService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public SessionService(IUnitOfWork uow, IMapper mapper) 
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PaginatedResult<SessionResponse>> GetAllSessionByEventIdAsync(Guid eventId, int page = 1, int size = 10)
    {
        var rs = await _uow.Sessions.GetAllSessionsByEventIdAsync(eventId, page, size);
        var data = rs.Data.Select(lg=>_mapper.Map<SessionResponse>(lg));
        return new PaginatedResult<SessionResponse>()
        {
            Data = data,
            Total = rs.Total,
            Page = rs.Page,
            Size = rs.Size
        };
    }

    public async Task<SessionResponse> CreateSessionAsync(Guid userId, Guid orgId, Guid eventId, CreateSessionDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            var chart = await _uow.Charts.GetChartByIdEventIdAsync(eventId,dto.ChartId);
            ChartGuard.EnsureExist(chart);
            var session = _mapper.Map<Session>(dto);
            session.ValidateSessionTime();
            var hasOverlap = await _uow.Sessions.CheckOverlappedAsync(eventId, session.Id,session.StartTime, session.EndTime);
            if (hasOverlap)
                throw new BusinessException("There is overlapping session time in event");
            await _uow.Sessions.AddAsync(eventId,session);
            await _uow.CommitAsync();

            return _mapper.Map<SessionResponse>(session);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            await _uow.RollbackAsync();
            throw new BusinessException($"Event have already has session name {dto.Name}");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<SessionResponse> UpdateSessionAsync(Guid userId, Guid orgId, Guid eventId, Guid sessionId, UpdateSessionDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            if (dto.ChartId.HasValue)
            {
                var chart = await _uow.Charts.GetChartByIdEventIdAsync(eventId,dto.ChartId);
                ChartGuard.EnsureExist(chart);
            }

            var session = await _uow.Sessions.GetByIdAsync(sessionId);
            SessionGuards.EnsureExist(session);
            session.Update(dto.Name,dto.StartTime,dto.EndTime,dto.ChartId);
            var hasOverlap = await _uow.Sessions.CheckOverlappedAsync(eventId, session.Id,session.StartTime, session.EndTime);
            if (hasOverlap)
                throw new BusinessException("There is overlapping session time in event");
            var updatedSession = await _uow.Sessions.UpdateAsync(session);
            if (updatedSession == 0)
            {
                throw new  BusinessException("Session update failed");
            }
 
            await _uow.CommitAsync();

            return _mapper.Map<SessionResponse>(session);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            await _uow.RollbackAsync();
            throw new BusinessException($"Event have already has session name {dto.Name}");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteSessionAsync(Guid eventId, Guid orgId, Guid sessionId)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evn = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evn);
            EventGuards.EnsureOwner(evn, orgId);
            var affected = await _uow.Sessions.DeleteAsync(
                eventId,
                orgId,
                sessionId);

            if (affected == 0)
                throw new BusinessException("Cannot delete session");
            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }
}


