namespace Eventiq.Contracts;

public record PasswordResetRequested
{
    public string EmailAddress { get; set; }
    public string ResetUrl { get; set; }
    public DateTime ExpireAt { get; set; }
}
