using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using WebhookReceiver.Services;
using Xunit;

namespace WebhookReceiver.Tests;

public class SignatureVerifierTests
{
    private static SignatureVerifier BuildVerifier(string secret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webhook:Secret"] = secret
            })
            .Build();
        return new SignatureVerifier(config);
    }

    private static string ComputeHmac(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void Valid_signature_is_accepted()
    {
        var body = """{"eventId":"evt_1","type":"test"}""";
        var secret = "supersecret";
        var signature = ComputeHmac(secret, body);

        Assert.True(BuildVerifier(secret).IsValid(body, signature));
    }

    [Fact]
    public void Tampered_body_is_rejected()
    {
        var body = """{"eventId":"evt_1"}""";
        var secret = "supersecret";
        var signature = ComputeHmac(secret, body);

        Assert.False(BuildVerifier(secret).IsValid(body + "x", signature));
    }

    [Fact]
    public void Missing_signature_is_rejected()
    {
        Assert.False(BuildVerifier("supersecret").IsValid("{}", null));
    }
}
