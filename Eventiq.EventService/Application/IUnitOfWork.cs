using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;

namespace Eventiq.EventService;

public interface IUnitOfWork: IDisposable
{
    public ILegendRepository Legends { get; }
    public IEventRepository Events { get; }
    public ISubmissionRepository Submissions { get; }
    public IChartRepository Charts { get; }
    public ISessionRepository Sessions { get; }

    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}