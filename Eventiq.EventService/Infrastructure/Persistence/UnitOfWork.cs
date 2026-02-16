using System.Data;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

namespace Eventiq.EventService.Infrastructure.Persistence;

public class UnitOfWork:IUnitOfWork
{
    private readonly IDbConnection _connection;
    private IDbTransaction _transaction;

    public UnitOfWork(IDbConnection connection, IDbTransaction transaction, ILegendRepository legends, IEventRepository events, ISubmissionRepository submissions, IChartRepository charts, ISessionRepository sessions)
    {
        _connection = connection;
        _transaction = transaction;
        Legends = legends;
        Events = events;
        Submissions = submissions;
        Charts = charts;
        Sessions = sessions;
    }

    public ILegendRepository Legends { get; }
    public IEventRepository Events { get; }
    public ISubmissionRepository Submissions { get; }
    public IChartRepository Charts { get; }
    public ISessionRepository Sessions { get; }

    public async Task BeginTransactionAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        _transaction = _connection.BeginTransaction();
        SetTransactionForRepositories();
    }

    private void SetTransactionForRepositories()
    {
        if (Legends is BaseRepository lg)
            lg.SetTransaction(_transaction);

        if (Events is BaseRepository ev)
            ev.SetTransaction(_transaction);

        if (Submissions is BaseRepository sms)
            sms.SetTransaction(_transaction);
        
        if (Sessions is BaseRepository ss)
            ss.SetTransaction(_transaction);
        
        if (Charts is BaseRepository c)
            c.SetTransaction(_transaction);
    }

    public async Task CommitAsync()
    {
        _transaction?.Commit();
        Dispose();
    }

    public async Task RollbackAsync()
    {
        _transaction?.Rollback();
        Dispose();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }
}