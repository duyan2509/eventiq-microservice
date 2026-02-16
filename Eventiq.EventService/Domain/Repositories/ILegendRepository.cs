using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Domain.Repositories;

public interface ILegendRepository
{
    Task<PaginatedResult<LegendModel>> GetAllLegendsByEventIdAsync(Guid eventId, int page = 1, int size = 10);
    Task<int> AddAsync(Legend legend);
    Task<LegendModel> GetLegendByIdEventIdAsync(Guid legendId, Guid eventId);

    Task<LegendModel?> UpdatePartialAsync(Guid legendId, Guid eventId, UpdateLegendDto dto);
    Task<int> DeleteAsync(Guid eventId, Guid orgId, Guid legendId);
}
