using AutoMapper;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.UserService.Infrastructure.Persistence;

public class RoleRepository:IRoleRepository
{
    private readonly ILogger<RoleRepository> _logger;
    private readonly EvtUserDbContext _context;
    private readonly DbSet<Role?> _roles;

    public RoleRepository(ILogger<RoleRepository> logger, EvtUserDbContext context)
    {
        _context = context;
        _roles = _context.Roles;
        _logger = logger;
    }
    
    public async Task<Role?> GetRoleByName(string name)
    {
        return await _roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == name);
    }
}