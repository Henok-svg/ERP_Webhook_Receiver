namespace WebhookReceiver.Models;

public record WebhookEvent(
    string EventId,
    string Type,
    string RawPayload,
    DateTimeOffset ReceivedAt);
