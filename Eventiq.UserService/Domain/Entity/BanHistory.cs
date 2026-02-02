namespace Eventiq.UserService.Domain.Entity;

public class BanHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string? Reason { get; set; }
    public Guid BannedById { get; set; }
    public User BannedByUser { get; set; }
    
}