using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Domain.Repositories;

public interface IChartRepository
{
    Task<int> DeleteAsync(Guid eventId, Guid orgId, Guid chartId);
    Task<PaginatedResult<ChartModel>> GetAllChartsByEventIdAsync(Guid eventId, int page, int size);
    Task<int> AddAsync(Guid EventId, Chart chart);
    Task<ChartModel?> UpdatePartialAsync(Guid chartId, Guid eventId, UpdateChartDto dto);
}

