using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public interface ILegendService
{
    Task<PaginatedResult<LegendResponse>> GetAllLegendsByEventIdAsync(Guid eventId, int page = 1, int size = 10);
    Task<LegendResponse> CreateLegendAsync(Guid userId, Guid orgId, Guid eventId, CreateLegendDto dto);
    Task<LegendResponse> UpdateLegendAsync(Guid userId, Guid orgId, Guid eventId, Guid legendId, UpdateLegendDto dto);
    Task DeleteLegendAsync(Guid userId, Guid orgId, Guid legendId);
}


