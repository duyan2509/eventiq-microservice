namespace Eventiq.EmailService.Templates.Models;

public class PasswordResetTemplateModel
{
    public string EmailAddress { get; set; } = default!;
    public string ResetToken { get; set; } = default!;
    public DateTime ExpireAt { get; set; }
}
