using System.Data;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public class LegendRepository : BaseRepository, ILegendRepository
{
    public LegendRepository(IDbConnection connection) : base(connection)
    {
    }

    public Task<PaginatedResult<LegendModel>> GetAllLegendsByEventIdAsync(Guid eventId, int page = 1, int size = 10)
    {
        throw new NotImplementedException();
    }

    public Task<int> AddAsync(Legend legend)
    {
        throw new NotImplementedException();
    }

    public Task<LegendModel> GetLegendByIdEventIdAsync(Guid legendId, Guid eventId)
    {
        throw new NotImplementedException();
    }

    public Task<LegendModel?> UpdatePartialAsync(Guid legendId, Guid eventId, UpdateLegendDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<int> DeleteAsync(Guid eventId, Guid orgId, Guid legendId)
    {
        throw new NotImplementedException();
    }
}
