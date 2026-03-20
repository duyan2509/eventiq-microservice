using Eventiq.SeatService.Domain.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eventiq.SeatService.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly SeatDbContext _ctx;
    private IDbContextTransaction? _transaction;

    public ISeatMapRepository SeatMaps { get; }
    public ISeatSectionRepository Sections { get; }
    public ISeatRowRepository Rows { get; }
    public ISeatRepository Seats { get; }
    public ISeatObjectRepository Objects { get; }
    public ISeatMapVersionRepository Versions { get; }

    public UnitOfWork(
        SeatDbContext ctx,
        ISeatMapRepository seatMaps,
        ISeatSectionRepository sections,
        ISeatRowRepository rows,
        ISeatRepository seats,
        ISeatObjectRepository objects,
        ISeatMapVersionRepository versions)
    {
        _ctx = ctx;
        SeatMaps = seatMaps;
        Sections = sections;
        Rows = rows;
        Seats = seats;
        Objects = objects;
        Versions = versions;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync()
    {
        _transaction = await _ctx.Database.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _ctx.Dispose();
    }
}
