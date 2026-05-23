using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public class PlatformConfigRepository : IPlatformConfigRepository
{
    private readonly EvtOrganizationDbContext _context;

    public PlatformConfigRepository(EvtOrganizationDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformConfig> GetAsync(CancellationToken ct = default)
    {
        var config = await _context.PlatformConfigs.FindAsync([1], ct);
        if (config != null) return config;

        // Seed singleton on first access
        config = new PlatformConfig { Id = 1 };
        _context.PlatformConfigs.Add(config);
        await _context.SaveChangesAsync(ct);
        return config;
    }

    public Task UpdateAsync(PlatformConfig config, CancellationToken ct = default)
    {
        _context.PlatformConfigs.Update(config);
        return Task.CompletedTask;
    }
}
