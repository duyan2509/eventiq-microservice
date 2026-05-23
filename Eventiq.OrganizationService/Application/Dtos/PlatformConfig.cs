using System.ComponentModel.DataAnnotations;

namespace Eventiq.OrganizationService.Dtos;

public class PlatformConfigResponse
{
    public decimal CurrentFeeRate { get; set; }
    public decimal? PendingFeeRate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int PayoutDayOfMonth { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdatePlatformConfigRequest
{
    [Range(0, 0.30)]
    public decimal? PendingFeeRate { get; set; }

    [Range(1, 28)]
    public int? PayoutDayOfMonth { get; set; }
}

public class InternalPlatformConfigResponse
{
    public decimal CurrentFeeRate { get; set; }
    public int PayoutDayOfMonth { get; set; }
}
