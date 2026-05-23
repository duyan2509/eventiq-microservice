using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Application.Service;

public class PlatformConfigService : IPlatformConfigService
{
    private readonly IPlatformConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public PlatformConfigService(IPlatformConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<PlatformConfigResponse> GetAsync(CancellationToken ct = default)
    {
        var config = await _repo.GetAsync(ct);
        return ToResponse(config);
    }

    public async Task<PlatformConfigResponse> UpdateAsync(Guid adminId, UpdatePlatformConfigRequest request, CancellationToken ct = default)
    {
        var config = await _repo.GetAsync(ct);

        if (request.PendingFeeRate.HasValue)
        {
            config.PendingFeeRate = request.PendingFeeRate.Value;
            config.EffectiveDate = NextPayoutDate(config.PayoutDayOfMonth);
        }

        if (request.PayoutDayOfMonth.HasValue)
            config.PayoutDayOfMonth = request.PayoutDayOfMonth.Value;

        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedBy = adminId;

        await _repo.UpdateAsync(config, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(config);
    }

    public async Task<InternalPlatformConfigResponse> GetInternalAsync(CancellationToken ct = default)
    {
        var config = await _repo.GetAsync(ct);
        return new InternalPlatformConfigResponse
        {
            CurrentFeeRate = config.CurrentFeeRate,
            PayoutDayOfMonth = config.PayoutDayOfMonth
        };
    }

    public async Task PromotePendingIfDueAsync(CancellationToken ct = default)
    {
        var config = await _repo.GetAsync(ct);

        if (config.PendingFeeRate == null || config.EffectiveDate == null)
            return;

        if (DateTime.UtcNow.Date < config.EffectiveDate.Value.Date)
            return;

        config.CurrentFeeRate = config.PendingFeeRate.Value;
        config.PendingFeeRate = null;
        config.EffectiveDate = null;
        config.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(config, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static DateTime NextPayoutDate(int dayOfMonth)
    {
        var now = DateTime.UtcNow;
        var candidate = new DateTime(now.Year, now.Month, dayOfMonth, 0, 0, 0, DateTimeKind.Utc);
        // If we're already past that day this month, schedule for next month
        if (candidate <= now)
            candidate = candidate.AddMonths(1);
        return candidate;
    }

    private static PlatformConfigResponse ToResponse(Domain.Entity.PlatformConfig c) => new()
    {
        CurrentFeeRate = c.CurrentFeeRate,
        PendingFeeRate = c.PendingFeeRate,
        EffectiveDate = c.EffectiveDate,
        PayoutDayOfMonth = c.PayoutDayOfMonth,
        UpdatedAt = c.UpdatedAt
    };
}
