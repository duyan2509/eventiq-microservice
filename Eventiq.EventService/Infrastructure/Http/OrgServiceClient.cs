namespace Eventiq.EventService.Infrastructure.Http;

public class OrgServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OrgServiceClient> _logger;

    public OrgServiceClient(HttpClient http, ILogger<OrgServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if the organization has an active Stripe payment account.
    /// Calls GET /internal/organizations/{orgId}/payment-status
    /// </summary>
    public async Task<bool> HasActivePaymentAsync(Guid orgId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/internal/organizations/{orgId}/payment-status", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OrgService returned {Status} for org {OrgId} payment check", response.StatusCode, orgId);
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<PaymentStatusDto>(cancellationToken: ct);
            return body?.IsActive == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check payment status for org {OrgId}", orgId);
            // Fail-safe: nếu không gọi được OrgService thì block submit
            return false;
        }
    }
}

public record PaymentStatusDto(bool IsActive);
