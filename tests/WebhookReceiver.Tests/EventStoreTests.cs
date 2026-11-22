using Microsoft.Extensions.Configuration;
using WebhookReceiver.Models;
using WebhookReceiver.Services;
using Xunit;

namespace WebhookReceiver.Tests;

public class EventStoreTests
{
    private static EventStore BuildStore()
    {
        return BuildStore(Path.Combine(
            Path.GetTempPath(),
            $"webhook-store-{Guid.NewGuid():N}.db"));
    }

    private static EventStore BuildStore(string databasePath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webhook:EncryptionKey"] = Convert.ToBase64String(new byte[32]),
                ["Webhook:Database"] = databasePath
            })
            .Build();
        return new EventStore(config);
    }

    [Fact]
    public void Same_event_is_stored_only_once()
    {
        var store = BuildStore();
        var evt = new WebhookEvent("evt_1", "test", "{}", DateTimeOffset.UtcNow);

        Assert.True(store.TryStore(evt));
        Assert.False(store.TryStore(evt));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Different_events_are_stored()
    {
        var store = BuildStore();

        Assert.True(store.TryStore(
            new WebhookEvent("evt_1", "test", "{}", DateTimeOffset.UtcNow)));
        Assert.True(store.TryStore(
            new WebhookEvent("evt_2", "test", "{}", DateTimeOffset.UtcNow)));
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Event_id_is_persisted_across_store_instances()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"webhook-store-{Guid.NewGuid():N}.db");
        var evt = new WebhookEvent("evt_1", "test", "{}", DateTimeOffset.UtcNow);

        var firstStore = BuildStore(databasePath);
        Assert.True(firstStore.TryStore(evt));

        var restartedStore = BuildStore(databasePath);
        Assert.False(restartedStore.TryStore(evt));
        Assert.Equal(1, restartedStore.Count);
    }
}
