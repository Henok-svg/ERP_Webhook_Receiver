# ERP Webhook Receiver

A generic, production-minded **.NET 8** webhook receiver that demonstrates the core
patterns used for ERP integration pipelines:

- **HMAC signature verification** - reject unsigned or tampered payloads
- **Idempotency** - deduplicate retried deliveries using an event key
- **Encrypted-at-rest storage** - persist payloads with AES-GCM encryption
- **Persistent idempotency** - store event records in SQLite across restarts
- **Clean minimal API** - explicit request/response contracts

> This is a sanitized reference implementation.
> logic is included. It exists to show the shape of a secure webhook ingestion service.

## Why this exists

ERP and Third party systems often deliver events via webhooks. Those deliveries can be
retried, replayed, or forged. This repository demonstrates:

1. Verify the signature before parsing anything.
2. Treat every delivery as idempotent.
3. Encrypt stored payloads so a database leak does not leak business data.
4. Keep the handler thin and testable.

## Run locally

```bash
cd src/WebhookReceiver
dotnet run
```

Send a test event:

```bash
curl -X POST http://localhost:5000/webhooks/erp \
  -H "Content-Type: application/json" \
  -H "X-Signature: <computed-hmac>" \
  -d '{"eventId":"evt_001","type":"shipment.created","data":{}}'
```

## Security notes

- Signatures use HMAC-SHA256 with a shared secret from configuration.
- Stored payloads are encrypted with AES-GCM; the nonce and tag are stored alongside ciphertext.
- `eventId` uniqueness enforces idempotency at the store layer.
- Webhook events are persisted in the SQLite database configured by `Webhook:Database`.
- Use HTTPS and a real secret management system in production.

Generate a valid encryption key:

```bash
openssl rand -base64 32
```

Paste the output into `Webhook:EncryptionKey` in `appsettings.json` or provide it through
configuration/environment variables.

## Tests

```bash
dotnet test
```

## License

MIT
