namespace Eventiq.Contracts;

public record PasswordResetRequested
{
    public string EmailAddress { get; set; }
    public string ResetToken { get; set; }
    public DateTime ExpireAt { get; set; }
}
