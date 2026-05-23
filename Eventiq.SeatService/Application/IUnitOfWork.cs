using Eventiq.SeatService.Domain.Repositories;

namespace Eventiq.SeatService;

public interface IUnitOfWork : IDisposable
{
    ISeatMapRepository SeatMaps { get; }
    ISeatRepository Seats { get; }
    ISeatObjectRepository Objects { get; }
    ISeatMapVersionRepository Versions { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
