using System.Data;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public class EventRepository : BaseRepository, IEventRepository
{
    public EventRepository(IDbConnection connection) : base(connection)
    {
    }

    public Task<EventModel> GetByIdAsync(Guid eventId)
    {
        throw new NotImplementedException();
    }
}
