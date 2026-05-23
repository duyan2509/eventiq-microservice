using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Repositories;

namespace Eventiq.SeatService.Infrastructure.BackgroundServices;

public class HoldExpiryCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HoldExpiryCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    public HoldExpiryCleanupService(IServiceScopeFactory scopeFactory, ILogger<HoldExpiryCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            await ReleaseExpiredHoldsAsync(stoppingToken);
        }
    }

    private async Task ReleaseExpiredHoldsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISeatRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var broadcaster = scope.ServiceProvider.GetRequiredService<ISeatStatusBroadcaster>();

            var expired = await repo.GetExpiredHoldingAsync(DateTime.UtcNow);
            if (expired.Count == 0) return;

            foreach (var seat in expired)
                seat.Release();

            await uow.SaveChangesAsync();

            var bySeatMap = expired
                .GroupBy(s => s.SeatMapId)
                .ToList();

            foreach (var group in bySeatMap)
            {
                var updates = group.Select(s => new SeatStatusUpdate(s.Id, "Available"));
                await broadcaster.BroadcastSeatStatusAsync(group.Key, updates);
            }

            _logger.LogInformation("Released {Count} expired seat holds across {Maps} seat maps.",
                expired.Count, bySeatMap.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error releasing expired seat holds.");
        }
    }
}
