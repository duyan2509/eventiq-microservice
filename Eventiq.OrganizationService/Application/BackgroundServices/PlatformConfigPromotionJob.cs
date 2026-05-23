using Eventiq.OrganizationService.Application.Service;

namespace Eventiq.OrganizationService.Application.BackgroundServices;

public class PlatformConfigPromotionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlatformConfigPromotionJob> _logger;

    public PlatformConfigPromotionJob(IServiceScopeFactory scopeFactory, ILogger<PlatformConfigPromotionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1); // run once daily at midnight UTC
            await Task.Delay(nextRun - now, stoppingToken);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var svc = scope.ServiceProvider.GetRequiredService<IPlatformConfigService>();
                await svc.PromotePendingIfDueAsync(stoppingToken);
                _logger.LogInformation("Platform config promotion check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during platform config promotion");
            }
        }
    }
}
