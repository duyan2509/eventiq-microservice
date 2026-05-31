namespace Eventiq.UserService.Domain.Entity;

public class PasswordResetToken:BaseEntity
{
    /// <summary>SHA-256 hash of the raw token sent in the reset URL.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public bool IsUsed { get; set; } = false;
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
}

