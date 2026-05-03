namespace Eventiq.EventService.Domain.Entity;


public class OrgPaymentInfo
{
    public Guid OrganizationId { get; set; }

    public string StripeAccountId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime UpdatedAt { get; set; }
}
