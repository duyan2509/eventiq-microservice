namespace Eventiq.UserService.Model;

public class RefreshTokenModel
{
    public string Token { get; set; }
    public Guid UserId { get; set; }
    public DateTime Expires { get; set; }

}