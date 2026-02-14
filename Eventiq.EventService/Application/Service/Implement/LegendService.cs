using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public class LegendService : ILegendService
{
    public Task<PaginatedResult<LegendResponse>> GetAllLegendsByEventIdAsync(Guid eventId, int page = 1, int size = 10)
    {
        throw new NotImplementedException();
    }

    public Task<LegendResponse> CreateLegendAsync(Guid userId, Guid orgId, Guid eventId, CreateLegendDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<LegendResponse> UpdateLegendAsync(Guid userId, Guid orgId, Guid eventId, Guid legendId, UpdateLegendDto dto)
    {
        throw new NotImplementedException();
    }

    public Task DeleteLegendAsync(Guid userId, Guid orgId, Guid legendId)
    {
        throw new NotImplementedException();
    }
}
