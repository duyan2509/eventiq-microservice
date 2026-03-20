using Eventiq.Contracts;
using Eventiq.SeatService.Domain.Repositories;
using Eventiq.SeatService.Infrastructure.Persistence;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

/// <summary>
/// Consumes ChartDeleted event from EventService.
/// When a Chart is deleted, soft-delete the associated SeatMap and all its children.
/// </summary>
public class ChartDeletedConsumer : IConsumer<ChartDeleted>
{
    private readonly ILogger<ChartDeletedConsumer> _logger;
    private readonly IUnitOfWork _uow;

    public ChartDeletedConsumer(ILogger<ChartDeletedConsumer> logger, IUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task Consume(ConsumeContext<ChartDeleted> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Received ChartDeleted: ChartId={ChartId}, EventId={EventId}, OrgId={OrgId}",
            message.ChartId, message.EventId, message.OrganizationId);

        try
        {
            var seatMap = await _uow.SeatMaps.GetByChartIdAsync(message.ChartId);

            if (seatMap == null)
            {
                _logger.LogWarning("No seat map found for ChartId={ChartId}, skipping.", message.ChartId);
                return;
            }

            // Soft-delete the seat map (cascade will handle sections/rows/seats via DB)
            var deleted = await _uow.SeatMaps.DeleteAsync(seatMap.Id);

            if (deleted)
            {
                await _uow.SaveChangesAsync();
                _logger.LogInformation(
                    "Soft-deleted SeatMap {SeatMapId} for ChartId={ChartId}",
                    seatMap.Id, message.ChartId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to delete SeatMap for ChartId={ChartId}", message.ChartId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ChartDeleted for ChartId={ChartId}", message.ChartId);
            throw; // Let MassTransit retry
        }
    }
}
