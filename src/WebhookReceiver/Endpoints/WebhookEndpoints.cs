using System.Text.Json;
using WebhookReceiver.Models;
using WebhookReceiver.Services;

namespace WebhookReceiver.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/webhooks/erp", async (
            HttpRequest request,
            SignatureVerifier verifier,
            EventStore store) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync();

            var signature = request.Headers["X-Signature"].FirstOrDefault();
            if (!verifier.IsValid(rawBody, signature))
                return Results.Unauthorized();

            JsonElement parsed;
            try
            {
                using var json = JsonDocument.Parse(rawBody);
                parsed = json.RootElement.Clone();
            }
            catch (JsonException)
            {
                return Results.BadRequest("Invalid JSON");
            }

            if (parsed.ValueKind != JsonValueKind.Object)
                return Results.BadRequest("Invalid JSON object");

            var eventId = parsed.TryGetProperty("eventId", out var id) ? id.GetString() : null;
            var type = parsed.TryGetProperty("type", out var t) ? t.GetString() : null;

            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(type))
                return Results.BadRequest("Missing eventId or type");

            var evt = new WebhookEvent(eventId, type, rawBody, DateTimeOffset.UtcNow);
            var stored = store.TryStore(evt);

            return stored
                ? Results.Accepted(value: new { status = "accepted", eventId })
                : Results.Ok(new { status = "duplicate", eventId });
        })
        .Accepts<string>("application/json");
    }
}
