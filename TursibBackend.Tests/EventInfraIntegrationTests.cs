using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Client;
using TursibBackend.Services;
using TursibIngestion;
using Xunit;

namespace TursibBackend.Tests;

/// <summary>
/// Rulează doar când sunt setate TEST_KAFKA_BOOTSTRAP (ex. localhost:9092) și TEST_MQTT_HOST (ex. localhost):
/// verifică codul real împotriva unui broker Kafka (sau Redpanda) și a unui broker MQTT, nu a unor simulări.
/// </summary>
public sealed class EventInfraFactAttribute : FactAttribute
{
    public EventInfraFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_KAFKA_BOOTSTRAP"))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_MQTT_HOST")))
            Skip = "Set TEST_KAFKA_BOOTSTRAP and TEST_MQTT_HOST to run the Kafka/MQTT integration tests";
    }
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
        var subscriber = new MqttSubscriber(new MqttOptions { Host = MqttHost }, ingestor, NullLogger<MqttSubscriber>.Instance);
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
