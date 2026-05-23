namespace Eventiq.OrganizationService.Domain.Entity;

public class PlatformConfig
{
    public int Id { get; set; } = 1;
    public decimal CurrentFeeRate { get; set; } = 0.05m;
    public decimal? PendingFeeRate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int PayoutDayOfMonth { get; set; } = 1;
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
