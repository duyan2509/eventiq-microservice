namespace Eventiq.UserService.Domain.Entity;

public class RefreshToken:BaseEntity
{
    public string Token { get; set; }
    public DateTime Expires { get; set; }
    public Guid UserId { get; set; }
    public virtual User User { get; set; }
}

