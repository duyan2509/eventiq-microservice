using System.Security.Cryptography;
using System.Text;

namespace Eventiq.EventService.Application.Utility;

/// <summary>
/// Signs ticket IDs with HMAC-SHA256 so QR payloads can't be forged.
/// Format: {ticketId}.{base64url(hmac)}
/// </summary>
public class TicketSigner
{
    private readonly byte[] _key;

    public TicketSigner(IConfiguration config)
    {
        var secret = config["Ticket:SigningSecret"]
            ?? throw new InvalidOperationException("Ticket:SigningSecret is not configured.");
        if (secret.Length < 32)
            throw new InvalidOperationException("Ticket:SigningSecret must be at least 32 characters.");
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string Sign(Guid ticketId)
    {
        var id = ticketId.ToString("N"); // no dashes, shorter QR
        var sig = ComputeSignature(id);
        return $"{id}.{sig}";
    }

    public bool TryVerify(string? token, out Guid ticketId)
    {
        ticketId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.', 2);
        if (parts.Length != 2) return false;

        var id = parts[0];
        var providedSig = parts[1];
        var expectedSig = ComputeSignature(id);

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedSig),
                Encoding.UTF8.GetBytes(expectedSig)))
            return false;

        return Guid.TryParseExact(id, "N", out ticketId);
    }

    private string ComputeSignature(string ticketId)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(ticketId));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
