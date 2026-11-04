using System.Text;
using Firebase.Database;
using Firebase.Database.Query;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Tursib.Contracts;

namespace BusSimulator
{
    /// <summary>Where a simulated on-board device sends its position.</summary>
    interface IPositionPublisher : IAsyncDisposable
    {
        string Name { get; }
        Task StartAsync();
        Task PublishAsync(VehiclePosition position);
    }

    /// <summary>
    /// Legacy path: writes straight to Firebase Realtime Database
    /// (bus_locations/route_X_bus_Y). Kept as the fallback source for the map.
    /// </summary>
    sealed class FirebasePublisher : IPositionPublisher
    {
        private const int MaxRetries = 3;
        private readonly FirebaseClient firebase;

        public FirebasePublisher(string url) => firebase = new FirebaseClient(url);

        public string Name => "firebase";

        public Task StartAsync() => Task.CompletedTask;

        public async Task PublishAsync(VehiclePosition position)
        {
            var json = Encoding.UTF8.GetString(ContractJson.Serialize(position));
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await firebase.Child("bus_locations").Child(position.VehicleId).PutAsync(json);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxRetries)
                        Console.WriteLine($"❌ {position.VehicleId} Firebase error: {ex.Message}");
                    else
                        await Task.Delay(500 * attempt);
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            firebase.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Publishes like a device on the bus: one MQTT topic per vehicle,
    /// QoS 1, reconnecting automatically. Messages queue while the broker is down
    /// and the oldest are dropped when the queue is full (stale positions are useless).
    /// </summary>
    sealed class MqttPublisher : IPositionPublisher
    {
        private readonly IManagedMqttClient client = new MqttFactory().CreateManagedMqttClient();
        private readonly string host;
        private readonly int port;

        public MqttPublisher(string host, int port)
        {
            this.host = host;
            this.port = port;
            client.ConnectedAsync += _ =>
            {
                Console.WriteLine($"🔌 MQTT connected to {host}:{port}");
                return Task.CompletedTask;
            };
            client.DisconnectedAsync += e =>
            {
                if (e.ClientWasConnected)
                    Console.WriteLine($"⚠️ MQTT disconnected: {e.Reason}");
                return Task.CompletedTask;
            };
        }

        public string Name => "mqtt";

        public Task StartAsync()
        {
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithMaxPendingMessages(2000)
                .WithPendingMessagesOverflowStrategy(MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithTcpServer(host, port)
                    .WithClientId($"tursib-sim-{Guid.NewGuid():N}")
                    .WithCleanSession()
                    .Build())
                .Build();
            return client.StartAsync(options);
        }

        public Task PublishAsync(VehiclePosition position)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(Topics.MqttPosition(position.RouteId, position.VehicleId))
                .WithPayload(ContractJson.Serialize(position))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            return client.EnqueueAsync(message);
        }

        public async ValueTask DisposeAsync()
        {
            await client.StopAsync();
            client.Dispose();
        }
    }

    /// <summary>Sends every position to all configured targets (PUBLISH_TARGETS).</summary>
    sealed class CompositePublisher : IPositionPublisher
    {
        private readonly IReadOnlyList<IPositionPublisher> targets;

        public CompositePublisher(IReadOnlyList<IPositionPublisher> targets) => this.targets = targets;

        public string Name => string.Join(",", targets.Select(t => t.Name));

        public Task StartAsync() => Task.WhenAll(targets.Select(t => t.StartAsync()));

        public Task PublishAsync(VehiclePosition position) =>
            Task.WhenAll(targets.Select(t => t.PublishAsync(position)));

        public async ValueTask DisposeAsync()
        {
            foreach (var target in targets)
                await target.DisposeAsync();
        }

        /// <summary>
        /// Builds the publisher from PUBLISH_TARGETS ("firebase", "mqtt" or "mqtt,firebase").
        /// Defaults to firebase so existing deployments keep working.
        /// </summary>
        public static CompositePublisher FromEnvironment(string firebaseUrl)
        {
            var raw = Environment.GetEnvironmentVariable("PUBLISH_TARGETS");
            var names = (string.IsNullOrWhiteSpace(raw) ? "firebase" : raw)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => n.ToLowerInvariant())
                .Distinct();

            var targets = new List<IPositionPublisher>();
            foreach (var name in names)
            {
                switch (name)
                {
                    case "firebase":
                        targets.Add(new FirebasePublisher(firebaseUrl));
                        break;
                    case "mqtt":
                        var host = Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost";
                        var port = int.TryParse(Environment.GetEnvironmentVariable("MQTT_PORT"), out var p) ? p : 1883;
                        targets.Add(new MqttPublisher(host, port));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown PUBLISH_TARGETS entry '{name}' (use firebase and/or mqtt)");
                }
            }
            return new CompositePublisher(targets);
        }
    }
}
