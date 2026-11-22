using System.Security.Cryptography;
using System.Text;

namespace WebhookReceiver.Services;

public class SignatureVerifier
{
    private readonly byte[] _secret;

    public SignatureVerifier(IConfiguration config)
    {
        var secret = config["Webhook:Secret"]
            ?? throw new InvalidOperationException("Webhook:Secret not configured");
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public bool IsValid(string rawBody, string? providedSignature)
    {
        if (string.IsNullOrWhiteSpace(providedSignature))
            return false;

        using var hmac = new HMACSHA256(_secret);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expected = Convert.ToHexString(computed).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant()));
    }
}
