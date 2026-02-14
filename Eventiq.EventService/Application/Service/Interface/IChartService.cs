using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public interface IChartService
{
    Task<PaginatedResult<ChartResponse>> GetAllChartsByEventIdAsync(Guid eventId, int page = 1, int size = 10);
    Task<ChartResponse> CreateChartAsync(Guid userId, Guid orgId, Guid eventId, CreateChartDto dto);
    Task<ChartResponse> UpdateChartAsync(Guid userId, Guid orgId, Guid eventId, Guid chartId, UpdateLChartDto dto);
    Task DeleteChartAsync(Guid userId, Guid orgId, Guid chartId);
}

