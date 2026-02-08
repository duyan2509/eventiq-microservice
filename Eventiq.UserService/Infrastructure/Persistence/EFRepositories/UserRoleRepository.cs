using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.UserService.Infrastructure.Persistence;

public class UserRoleRepository: IUserRoleRepository
{
    private readonly EvtUserDbContext _context;
    private readonly DbSet<UserRole> _userRoles;
    private readonly ILogger<UserRoleRepository> _logger;
    public UserRoleRepository(EvtUserDbContext context,
        ILogger<UserRoleRepository> logger)
    {
        _context = context;
        _userRoles = context.UserRoles;
        _logger = logger;
    }
    public async Task AddUserRole(UserRole userRole)
    {
        await _userRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();
    }

    public async Task<UserRole?> GetUserRoleByRoleIdNOrgId(Guid roleId, Guid orgId)
    {
        return await  _userRoles.AsNoTracking()
            .Where(ur=> ur.OrganizationId == orgId && ur.RoleId == roleId )
            .FirstOrDefaultAsync();
    }
}