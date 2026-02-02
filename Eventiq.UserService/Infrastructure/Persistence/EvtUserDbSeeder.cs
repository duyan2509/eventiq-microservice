using Eventiq.UserService.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Helper;

namespace Eventiq.UserService.Infrastructure.Persistence
{
    public static class EvtUserDbSeeder
    {
        public static async Task SeedAsync(EvtUserDbContext context, ILogger logger, RegisterDto admin)
        {
            await context.Database.MigrateAsync();

            if (await context.Roles.AnyAsync())
            {
                logger.LogInformation("Database already seeded");
                return;
            }

            //  Seed roles
            var adminRole = new Role { Id = Guid.NewGuid(), Name = nameof(AppRoles.Admin) };
            var userRole = new Role { Id = Guid.NewGuid(), Name = nameof(AppRoles.User) };
            var staffRole = new Role { Id = Guid.NewGuid(), Name = nameof(AppRoles.Staff) };
            var orgRole = new Role { Id = Guid.NewGuid(), Name = nameof(AppRoles.Organization) };

            context.Roles.AddRange(adminRole, userRole, staffRole, orgRole);

            //  Seed admin user
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = admin.Email,
                PasswordHash = PasswordHash.SHA256Hash(admin.Password),
                Avatar = ""
            };

            context.Users.Add(adminUser);

            //  Assign admin role
            context.UserRoles.Add(new UserRole
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id
            });

            await context.SaveChangesAsync();

            logger.LogInformation("Seeded admin account & roles successfully");
        }


    }
}
