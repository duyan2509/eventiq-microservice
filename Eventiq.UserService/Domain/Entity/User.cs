using Eventiq.UserService.Domain.Enums;

namespace Eventiq.UserService.Domain.Entity;

public class User:BaseEntity
{
    public string Email { get; set; }
    public string Username { get; set; }
    public bool IsBanned { get; set; } = false;
    public string PasswordHash { get; set; }
    public string Avatar { get; set; } = "";
    public virtual ICollection<UserRole> UserRoles { get; set; }
    public virtual PasswordResetToken  PasswordResetToken { get; set; }
    public virtual ICollection<BanHistory> BanHistories { get; set; }
    public virtual ICollection<BanHistory> BannedUsers { get; set; }


}

