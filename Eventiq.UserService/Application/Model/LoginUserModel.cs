using Eventiq.UserService.Domain.Enums;

namespace Eventiq.UserService.Model;

public class LoginUserModel
{
    public string Id { get; set; }
    public string Email  { get; set; }
    public string PasswordHash { get; set;  }
    public bool IsBanned { get; set; }
    public IReadOnlyList<string> Roles { get; init; } = [];

}