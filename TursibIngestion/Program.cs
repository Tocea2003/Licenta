using TursibIngestion;

// Serviciul de ingestie: MQTT (dispozitive din autobuze) -> validare -> Kafka, topicul vehicle-positions.
// Configurare prin variabile de mediu (Mqtt__Host, Kafka__BootstrapServers, ...) sau appsettings.json.

var builder = Host.CreateApplicationBuilder(args);

var mqtt = builder.Configuration.GetSection("Mqtt").Get<MqttOptions>() ?? new MqttOptions();
var kafka = builder.Configuration.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions();

builder.Services.AddSingleton(mqtt);
builder.Services.AddSingleton(kafka);
builder.Services.AddSingleton<KafkaPositionSink>();
builder.Services.AddSingleton<IPositionSink>(sp => sp.GetRequiredService<KafkaPositionSink>());
builder.Services.AddSingleton<IngestionMetrics>();
builder.Services.AddSingleton<PositionIngestor>();
builder.Services.AddHostedService<TopicBootstrapper>();
builder.Services.AddHostedService<MqttSubscriber>();
builder.Services.AddHostedService<MetricsLogger>();

await builder.Build().RunAsync();

namespace TursibIngestion
{
    /// <summary>Creează topicurile Kafka la pornire; reîncearcă până când brokerul răspunde.</summary>
    public class TopicBootstrapper : BackgroundService
    {
        private readonly KafkaPositionSink _sink;
        private readonly ILogger<TopicBootstrapper> _logger;

        public TopicBootstrapper(KafkaPositionSink sink, ILogger<TopicBootstrapper> logger)
        {
            _sink = sink;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await _sink.EnsureTopicsAsync(_logger, stoppingToken); return; }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Kafka indisponibil ({Message}); reîncerc în 5 s", ex.Message);
                    try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }
    }
}
