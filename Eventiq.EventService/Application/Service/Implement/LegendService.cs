using AutoMapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Extensions;
using Eventiq.EventService.Guards;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.EventService.Application.Service;

public class LegendService : ILegendService
{
    private readonly IUnitOfWork _uow;

    public LegendService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    private readonly IMapper _mapper;
    public async Task<PaginatedResult<LegendResponse>> GetAllLegendsByEventIdAsync(Guid eventId, int page = 1, int size = 10)
    {
        var rs = await _uow.Legends.GetAllLegendsByEventIdAsync(eventId, page, size);
        var data = rs.Data.Select(lg=>_mapper.Map<LegendResponse>(lg));
        return new PaginatedResult<LegendResponse>()
        {
            Data = data,
            Total = rs.Total,
            Page = rs.Page,
            Size = rs.Size
        };
    }

    public async Task<LegendResponse> CreateLegendAsync(Guid userId, Guid orgId, Guid eventId, CreateLegendDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            var legend = _mapper.Map<Legend>(dto);
            legend.EventId = evt.Id;
            await _uow.Legends.AddAsync(legend);
            await _uow.CommitAsync();

            return _mapper.Map<LegendResponse>(legend);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            await _uow.RollbackAsync();
            throw new BusinessException($"Event have already has legend name {dto.Name}");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<LegendResponse> UpdateLegendAsync(Guid userId, Guid orgId, Guid eventId, Guid legendId, UpdateLegendDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            var updatedLegend = await _uow.Legends.UpdatePartialAsync(
                legendId,
                eventId,
                dto);

            LegendGuards.EnsureExist(updatedLegend);
 
            await _uow.CommitAsync();

            return _mapper.Map<LegendResponse>(updatedLegend);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            await _uow.RollbackAsync();
            throw new BusinessException($"Event have already has legend name {dto.Name}");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteLegendAsync(Guid eventId, Guid orgId, Guid legendId)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evn = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evn);
            EventGuards.EnsureOwner(evn, orgId);
            var affected = await _uow.Legends.DeleteAsync(
                eventId,
                orgId,
                legendId);

            if (affected == 0)
                throw new BusinessException("Cannot delete legend");

            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
        
    }
}
