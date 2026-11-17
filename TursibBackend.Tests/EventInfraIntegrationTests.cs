using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Client;
using TursibBackend.Services;
using TursibIngestion;
using TursibMessaging;
using Xunit;

namespace TursibBackend.Tests;

/// <summary>
/// Rulează doar când sunt setate TEST_KAFKA_BOOTSTRAP (ex. localhost:9092) și TEST_MQTT_HOST (ex. localhost):
/// verifică codul real împotriva unui broker Kafka (sau Redpanda) și a unui broker MQTT, nu a unor simulări.
/// </summary>
public class RequiresEnvFactAttribute : FactAttribute
{
    public RequiresEnvFactAttribute(params string[] variables)
    {
        var missing = variables.Where(v => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v))).ToList();
        if (missing.Count > 0) Skip = $"Set {string.Join(", ", missing)} to run this integration test";
    }
}

public sealed class EventInfraFactAttribute : RequiresEnvFactAttribute
{
    public EventInfraFactAttribute() : base("TEST_KAFKA_BOOTSTRAP", "TEST_MQTT_HOST") { }
}

/// <summary>Brokere cu TLS + autentificare (ca cele gestionate din cloud): TEST_MQTT_TLS_PORT, TEST_MQTT_CA, TEST_SECURE_USER/PASS.</summary>
public sealed class SecureMqttFactAttribute : RequiresEnvFactAttribute
{
    public SecureMqttFactAttribute() : base("TEST_MQTT_HOST", "TEST_MQTT_TLS_PORT", "TEST_MQTT_CA", "TEST_SECURE_USER", "TEST_SECURE_PASS") { }
}

/// <summary>Kafka cu SASL_SSL: TEST_KAFKA_SASL_BOOTSTRAP, TEST_MQTT_CA (același CA), TEST_SECURE_USER/PASS.</summary>
public sealed class SecureKafkaFactAttribute : RequiresEnvFactAttribute
{
    public SecureKafkaFactAttribute() : base("TEST_KAFKA_SASL_BOOTSTRAP", "TEST_MQTT_CA", "TEST_SECURE_USER", "TEST_SECURE_PASS") { }
}

public class MqttToKafkaIntegrationTests
{
    private static string Kafka => Environment.GetEnvironmentVariable("TEST_KAFKA_BOOTSTRAP")!;
    private static string MqttHost => Environment.GetEnvironmentVariable("TEST_MQTT_HOST")!;

    private static ConsumerConfig ConsumerConfig() => new()
    {
        BootstrapServers = Kafka, GroupId = $"test-{Guid.NewGuid():N}", AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false
    };

    [EventInfraFact]
    public async Task ValidMqttMessages_ReachKafka_AndInvalidOnesTheDeadLetterTopic()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var kafka = new TursibIngestion.KafkaOptions { BootstrapServers = Kafka, Topic = $"test-positions-{suffix}", DeadLetterTopic = $"test-dlq-{suffix}", Partitions = 2 };
        using var sink = new KafkaPositionSink(kafka);
        await sink.EnsureTopicsAsync(NullLogger.Instance, default);

        var ingestor = new PositionIngestor(sink, new IngestionMetrics(), NullLogger<PositionIngestor>.Instance);
        var subscriber = new MqttSubscriber(new MqttOptions { Host = MqttHost }, ingestor.HandleAsync, NullLogger<MqttSubscriber>.Instance);
        await subscriber.StartAsync(default);
        try
        {
            using var device = new MqttFactory().CreateMqttClient();
            await device.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(MqttHost).Build());
            var vehicle = $"route_77_bus_{suffix}";
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // brokerul și abonamentul se stabilesc asincron: publicăm până apare mesajul pe Kafka
            using var consumer = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
            consumer.Subscribe(kafka.Topic);
            ConsumeResult<string, string>? got = null;
            for (var attempt = 0; attempt < 40 && got == null; attempt++)
            {
                await device.PublishStringAsync($"tursib/vehicles/{vehicle}/position",
                    $$"""{"latitude":45.79,"longitude":24.15,"routeId":77,"timestamp":{{ts}},"speed":20}""");
                await device.PublishStringAsync($"tursib/vehicles/{vehicle}/position", "garbage");
                got = consumer.Consume(TimeSpan.FromMilliseconds(500));
            }

            Assert.NotNull(got);
            Assert.Equal(vehicle, got!.Message.Key);
            Assert.True(KafkaMessageParser.TryParse(got.Message.Value, out var position));
            Assert.Equal(77, position!.RouteId);

            using var dlq = new ConsumerBuilder<string, byte[]>(ConsumerConfig()).Build();
            dlq.Subscribe(kafka.DeadLetterTopic);
            var rejected = dlq.Consume(TimeSpan.FromSeconds(10));
            Assert.NotNull(rejected);
            Assert.Equal("InvalidJson", Encoding.UTF8.GetString(rejected!.Message.Headers.GetLastBytes("reason")));
            Assert.Equal("garbage", Encoding.UTF8.GetString(rejected.Message.Value));
        }
        finally
        {
            await subscriber.StopAsync(default);
        }
    }
}

public class KafkaToBackendIntegrationTests : IClassFixture<KafkaToBackendIntegrationTests.KafkaBackendFactory>
{
    public class KafkaBackendFactory : AuthWebAppFactory
    {
        public string Topic { get; } = $"test-backend-{Guid.NewGuid():N}"[..28];

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Kafka:BootstrapServers", Environment.GetEnvironmentVariable("TEST_KAFKA_BOOTSTRAP") ?? "");
            builder.UseSetting("Kafka:Topic", Topic);
            builder.UseSetting("Kafka:GroupId", $"test-group-{Guid.NewGuid():N}");
        }
    }

    private readonly KafkaBackendFactory _factory;
    public KafkaToBackendIntegrationTests(KafkaBackendFactory factory) => _factory = factory;

    [EventInfraFact]
    public async Task MessagesOnKafka_AppearInTheLiveSnapshot_AndTheGtfsRealtimeFeed()
    {
        var client = _factory.CreateClient();
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = Environment.GetEnvironmentVariable("TEST_KAFKA_BOOTSTRAP")
        }).Build();

        // Consumer-ul pornește de la „acum” (AutoOffsetReset=Latest): retrimitem până după ce primește partițiile.
        JsonElement snapshot = default;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await producer.ProduceAsync(_factory.Topic, new Message<string, string>
            {
                Key = "route_66_bus_0",
                Value = $$"""{"id":"route_66_bus_0","latitude":45.8,"longitude":24.2,"routeId":66,"timestamp":{{ts}},"speed":25,"directionId":0}"""
            });
            snapshot = await client.GetFromJsonAsync<JsonElement>("/api/live/positions");
            if (snapshot.EnumerateArray().Any(b => b.GetProperty("id").GetString() == "route_66_bus_0")) break;
            await Task.Delay(500);
        }

        Assert.Contains(snapshot.EnumerateArray(), b => b.GetProperty("id").GetString() == "route_66_bus_0");

        var feed = await client.GetFromJsonAsync<JsonElement>("/api/gtfs-rt/vehicle-positions?format=json");
        Assert.Contains(feed.GetProperty("entity").EnumerateArray(), e => e.GetProperty("id").GetString() == "route_66_bus_0");
    }
}


/// <summary>Ingestia MQTT integrată în backend, împotriva unui broker real: simulator → MQTT → backend, fără worker și fără Kafka.</summary>
public class EmbeddedMqttIntegrationTests : IClassFixture<EmbeddedMqttIntegrationTests.EmbeddedFactory>
{
    public class EmbeddedFactory : AuthWebAppFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Ingestion:EmbeddedMqtt", "true");
            builder.UseSetting("Mqtt:Host", Environment.GetEnvironmentVariable("TEST_MQTT_HOST") ?? "localhost");
        }
    }

    private readonly EmbeddedFactory _factory;
    public EmbeddedMqttIntegrationTests(EmbeddedFactory factory) => _factory = factory;

    [RequiresEnvFact("TEST_MQTT_HOST")]
    public async Task MqttMessages_ReachTheLiveSnapshotAndTheGtfsRealtimeFeed_WithoutKafka()
    {
        var client = _factory.CreateClient();
        using var device = new MqttFactory().CreateMqttClient();
        await device.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(Environment.GetEnvironmentVariable("TEST_MQTT_HOST")).Build());

        JsonElement snapshot = default;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await device.PublishStringAsync("tursib/vehicles/route_88_bus_0/position",
                $$"""{"latitude":45.8,"longitude":24.2,"routeId":88,"timestamp":{{ts}},"speed":22,"directionId":0}""");
            snapshot = await client.GetFromJsonAsync<JsonElement>("/api/live/positions");
            if (snapshot.EnumerateArray().Any(b => b.GetProperty("id").GetString() == "route_88_bus_0")) break;
            await Task.Delay(300);
        }

        Assert.Contains(snapshot.EnumerateArray(), b => b.GetProperty("id").GetString() == "route_88_bus_0");
    }
}

public class SecureBrokerIntegrationTests
{
    private static MqttOptions SecureMqtt(string? password = null, string? ca = null) => new()
    {
        Host = Environment.GetEnvironmentVariable("TEST_MQTT_HOST")!,
        Port = int.Parse(Environment.GetEnvironmentVariable("TEST_MQTT_TLS_PORT")!),
        UseTls = true,
        CaCertificatePath = ca ?? Environment.GetEnvironmentVariable("TEST_MQTT_CA"),
        Username = Environment.GetEnvironmentVariable("TEST_SECURE_USER"),
        Password = password ?? Environment.GetEnvironmentVariable("TEST_SECURE_PASS")
    };

    [SecureMqttFact]
    public async Task MqttSubscriber_ConnectsOverTlsWithCredentials_AndReceivesMessages()
    {
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = new MqttSubscriber(SecureMqtt(),
            (topic, payload, _) => { received.TrySetResult(topic); return Task.CompletedTask; }, NullLogger.Instance);
        await subscriber.StartAsync(default);
        try
        {
            using var device = new MqttFactory().CreateMqttClient();
            await device.ConnectAsync(SecureMqtt().Build("test-device"));
            for (var attempt = 0; attempt < 30 && !received.Task.IsCompleted; attempt++)
            {
                await device.PublishStringAsync("tursib/vehicles/route_9_bus_0/position", "{}");
                await Task.Delay(300);
            }
            Assert.Equal("tursib/vehicles/route_9_bus_0/position", await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { await subscriber.StopAsync(default); }
    }

    [SecureMqttFact]
    public async Task MqttConnection_IsRefused_ForAWrongPassword_OrAnUntrustedCertificate()
    {
        using var client = new MqttFactory().CreateMqttClient();
        await Assert.ThrowsAnyAsync<Exception>(() => client.ConnectAsync(SecureMqtt(password: "wrong").Build("test")));

        // fără certificatul CA propriu, certificatul brokerului nu e de încredere
        var untrusted = SecureMqtt();
        untrusted.CaCertificatePath = null;
        await Assert.ThrowsAnyAsync<Exception>(() => client.ConnectAsync(untrusted.Build("test")));
    }

    [SecureKafkaFact]
    public async Task Kafka_SaslSsl_ProducerAndConsumerWork_AndAWrongPasswordIsRefused()
    {
        var options = new TursibIngestion.KafkaOptions
        {
            BootstrapServers = Environment.GetEnvironmentVariable("TEST_KAFKA_SASL_BOOTSTRAP"),
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = Environment.GetEnvironmentVariable("TEST_SECURE_USER"),
            SaslPassword = Environment.GetEnvironmentVariable("TEST_SECURE_PASS"),
            SslCaLocation = Environment.GetEnvironmentVariable("TEST_MQTT_CA"),
            Topic = $"test-secure-{Guid.NewGuid():N}"[..24], DeadLetterTopic = $"test-secure-dlq-{Guid.NewGuid():N}"[..28]
        };
        using var sink = new KafkaPositionSink(options);
        await sink.EnsureTopicsAsync(NullLogger.Instance, default);
        await sink.PublishAsync("route_1_bus_0", """{"id":"route_1_bus_0"}""", default);

        var config = new ConsumerConfig { GroupId = $"g-{Guid.NewGuid():N}", AutoOffsetReset = AutoOffsetReset.Earliest };
        options.Apply(config);
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(options.Topic);
        var message = consumer.Consume(TimeSpan.FromSeconds(15));
        Assert.Equal("route_1_bus_0", message?.Message.Key);

        options.SaslPassword = "wrong";
        using var wrong = new KafkaPositionSink(options);
        await Assert.ThrowsAnyAsync<Exception>(() => wrong.PublishAsync("route_1_bus_0", "{}", new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token));
    }
}
