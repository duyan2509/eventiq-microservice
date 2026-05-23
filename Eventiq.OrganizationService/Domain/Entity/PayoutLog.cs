namespace Eventiq.OrganizationService.Domain.Entity;

public class PayoutLog
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public string StripePayoutId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
}
