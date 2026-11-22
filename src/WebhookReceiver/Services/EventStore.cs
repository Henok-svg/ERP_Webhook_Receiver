using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WebhookReceiver.Models;

namespace WebhookReceiver.Services;

/// <summary>
/// SQLite-backed, encrypted-at-rest event store.
/// </summary>
public class EventStore
{
    private readonly byte[] _key;
    private readonly string _connectionString;
    private readonly object _lock = new();

    public EventStore(IConfiguration config)
    {
        var keyB64 = config["Webhook:EncryptionKey"]
            ?? throw new InvalidOperationException("Webhook:EncryptionKey not configured");

        try
        {
            _key = Convert.FromBase64String(keyB64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("EncryptionKey must be a valid Base64-encoded key.", ex);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption key must be 32 bytes (AES-256).");

        var databasePath = config["Webhook:Database"]
            ?? Path.Combine(AppContext.BaseDirectory, "webhooks.db");

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS webhook_events (
                event_id TEXT PRIMARY KEY,
                cipher BLOB NOT NULL,
                nonce BLOB NOT NULL,
                tag BLOB NOT NULL,
                received_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <returns>true if newly stored, false if eventId already exists (idempotent).</returns>
    public bool TryStore(WebhookEvent evt)
    {
        lock (_lock)
        {
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(evt);

            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            RandomNumberGenerator.Fill(nonce);

            var cipher = new byte[plaintext.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            using var aes = new AesGcm(_key, tag.Length);
            aes.Encrypt(nonce, plaintext, cipher, tag);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO webhook_events (event_id, cipher, nonce, tag, received_at)
                VALUES ($eventId, $cipher, $nonce, $tag, $receivedAt);
                """;
            command.Parameters.AddWithValue("$eventId", evt.EventId);
            command.Parameters.AddWithValue("$cipher", cipher);
            command.Parameters.AddWithValue("$nonce", nonce);
            command.Parameters.AddWithValue("$tag", tag);
            command.Parameters.AddWithValue("$receivedAt", evt.ReceivedAt.UtcDateTime.ToString("O"));

            try
            {
                return command.ExecuteNonQuery() == 1;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return false;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM webhook_events;";
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}
