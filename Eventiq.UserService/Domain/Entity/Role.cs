namespace Eventiq.UserService.Domain.Entity;

public class Role : BaseEntity
{
    public string Name { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; }
}


