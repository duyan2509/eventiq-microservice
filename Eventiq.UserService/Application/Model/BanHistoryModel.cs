namespace Eventiq.UserService.Model;

public class BanHistoryModel
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; }
    public string Reason { get; set; }
    public Guid AdminId { get; set; }
    public string AdminEmail { get; set; }
}