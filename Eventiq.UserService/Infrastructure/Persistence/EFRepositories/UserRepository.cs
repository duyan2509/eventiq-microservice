using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Model;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.UserService.Infrastructure.Persistence;

public class UserRepository:IUserRepository
{
    private  EvtUserDbContext Context { get; }
    private readonly ILogger<UserRepository> _logger;
    private DbSet<User> _users;

    public UserRepository(ILogger<UserRepository> logger, DbSet<User> users, EvtUserDbContext context)
    {
        _logger = logger;
        _users = users;
        Context = context;
    }

    public async Task<LoginUserModel> GetUserByEmail(string email)
    {
        var rs = await _users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u=> new LoginUserModel
            {
                Id = u.Id.ToString(),
                Email = u.Email,
                PasswordHash = u.PasswordHash,
                IsBanned = u.IsBanned,
                Roles = u.UserRoles.Select(ur=>ur.Role.Name).ToList(),
                
            })
            .FirstOrDefaultAsync();
        return rs;
    }

    public async Task<LoginUserModel> GetUserById(Guid userId)
    {
        var rs = await _users
            .AsNoTracking()
            .Where(u => u.Id==userId)
            .Select(u=> new LoginUserModel
            {
                Id = u.Id.ToString(),
                Email = u.Email,
                PasswordHash = u.PasswordHash,
                IsBanned = u.IsBanned,
                Roles = u.UserRoles.Select(ur=>ur.Role.Name).ToList(),
                
            })
            .FirstOrDefaultAsync();
        return rs;
    }

    public async Task AddUser(User user)
    {
        _users.Add(user);
        await Context.SaveChangesAsync();
    }

    public async Task UpdateUser(User user)
    {
        _users.Update(user);
        await Context.SaveChangesAsync();
    }

    public async Task<User?> GetTrackingUserById(Guid userId)
    {
        return await _users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<PaginatedResult<User>> GetAllUsers(int page, int size, string enail="")
    {
        if (page < 1) page = 1;
        if (size < 1) size = 1;
        
        var query = _users.AsNoTracking()
            .Where(u => u.Email.Contains(enail));
        int total = query.Count();
        var data =new List<User>();
        if(page*size<=total) 
            data = await  query.Skip((page - 1) * size).Take(size).ToListAsync();
        return new PaginatedResult<User>
        {
            Data = data,
            Total = total,
            Page = page,
            Size = size
        };
    }
}