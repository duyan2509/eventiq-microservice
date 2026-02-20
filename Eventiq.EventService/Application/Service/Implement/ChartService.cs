using AutoMapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Extensions;
using Eventiq.EventService.Guards;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.EventService.Application.Service;

public class ChartService : IChartService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ChartService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PaginatedResult<ChartResponse>> GetAllChartsByEventIdAsync(Guid eventId, int page = 1, int size = 10)
    {
        var rs = await _uow.Charts.GetAllChartsByEventIdAsync(eventId, page, size);
        var data = rs.Data.Select(lg=>_mapper.Map<ChartResponse>(lg));
        return new PaginatedResult<ChartResponse>()
        {
            Data = data,
            Total = rs.Total,
            Page = rs.Page,
            Size = rs.Size
        };
    }

    public async Task<ChartResponse> CreateChartAsync(Guid userId, Guid orgId, Guid eventId, CreateChartDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            var chart = _mapper.Map<Chart>(dto);
            await _uow.Charts.AddAsync(eventId,chart);
            await _uow.CommitAsync();

            return _mapper.Map<ChartResponse>(chart);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            await _uow.RollbackAsync();
            throw new BusinessException($"Event have already has chart name {dto.Name}");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }


    public async Task<ChartResponse> UpdateChartAsync(Guid userId, Guid orgId, Guid eventId, Guid chartId, UpdateChartDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evt = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evt);
            EventGuards.EnsureOwner(evt, orgId);
            var updatedChart = await _uow.Charts.UpdatePartialAsync(
                chartId,
                eventId,
                dto);

            ChartGuard.EnsureExist(updatedChart);
 
            await _uow.CommitAsync();

            return _mapper.Map<ChartResponse>(updatedChart);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            await _uow.RollbackAsync();
            throw new BusinessException($"Event have already has chart name {dto.Name}");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteChartAsync(Guid eventId, Guid orgId, Guid chartId)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var evn = await _uow.Events.GetByIdAsync(eventId);
            EventGuards.EnsureExist(evn);
            EventGuards.EnsureOwner(evn, orgId);
            var affected = await _uow.Charts.DeleteAsync(
                eventId,
                orgId,
                chartId);

            if (affected == 0)
                throw new BusinessException("Cannot delete chart");
            // publish message for seat deletion
            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
        
    }
}

public static class ChartGuard
{
    public static void EnsureExist(ChartModel chart)
    {
        if(chart ==  null)
            throw new NotFoundException("Chart is null");
    }
}
