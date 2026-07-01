using Eventiq.Contracts.Grpc;
using Eventiq.EventService.Application.Service;

namespace Eventiq.EventService.Infrastructure.Http;

public class SeatServiceClient : ISeatServiceClient
{
    private readonly SeatInternal.SeatInternalClient _grpcClient;
    private readonly ILogger<SeatServiceClient> _logger;

    public SeatServiceClient(SeatInternal.SeatInternalClient grpcClient, ILogger<SeatServiceClient> logger)
    {
        _grpcClient = grpcClient;
        _logger = logger;
    }

    public async Task<Guid?> InitSessionSeatMapAsync(Guid sessionId, Guid chartId, Guid eventId)
    {
        try
        {
            var response = await _grpcClient.InitSessionSeatMapAsync(new InitSessionSeatMapRequest
            {
                SessionId = sessionId.ToString(),
                ChartId = chartId.ToString(),
                EventId = eventId.ToString()
            });
            return response.Success && Guid.TryParse(response.SeatMapId, out var id) ? id : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to init seatmap for session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<bool> IsLegendUsedInTemplateAsync(Guid legendId)
    {
        try
        {
            var response = await _grpcClient.IsLegendUsedInTemplateAsync(new IsLegendUsedInTemplateRequest
            {
                LegendId = legendId.ToString()
            });
            return response.InUse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check legend usage for {LegendId}", legendId);
            return false;
        }
    }

    public async Task<bool> HasSeatMapForChartAsync(Guid chartId)
    {
        try
        {
            var response = await _grpcClient.CheckSeatMapForChartAsync(new CheckSeatMapForChartRequest
            {
                ChartId = chartId.ToString()
            });
            return response.HasSeatMap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check seat map for chart {ChartId}", chartId);
            return true; // fail open: không block tạo session nếu check lỗi
        }
    }
}
