namespace Eventiq.UserService.Domain.Entity;

public class PasswordResetToken:BaseEntity
{
    public string Token { get; set; }
    public DateTime Expires { get; set; }
    public Guid UserId { get; set; }
    public virtual User User { get; set; }
}

