using System.Data;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public abstract class BaseRepository
{
    protected readonly IDbConnection _connection;
    protected IDbTransaction _transaction;

    protected BaseRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public void SetTransaction(IDbTransaction transaction)
    {
        _transaction = transaction;
    }
}
