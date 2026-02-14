using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public class ChartService : IChartService
{
    public Task<PaginatedResult<ChartResponse>> GetAllChartsByEventIdAsync(Guid eventId, int page = 1, int size = 10)
    {
        throw new NotImplementedException();
    }

    public Task<ChartResponse> CreateChartAsync(Guid userId, Guid orgId, Guid eventId, CreateChartDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<ChartResponse> UpdateChartAsync(Guid userId, Guid orgId, Guid eventId, Guid chartId, UpdateLChartDto dto)
    {
        throw new NotImplementedException();
    }

    public Task DeleteChartAsync(Guid userId, Guid orgId, Guid chartId)
    {
        throw new NotImplementedException();
    }
}
