using TursibIngestion;

var builder = WebApplication.CreateBuilder(args);

var options = new IngestionOptions
{
    MqttHost = Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost",
    MqttPort = int.TryParse(Environment.GetEnvironmentVariable("MQTT_PORT"), out var port) ? port : 1883,
    KafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:19092"
};

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IngestionStats>();
builder.Services.AddHostedService<IngestionWorker>();

var app = builder.Build();

// Liveness + counters for the demo and for docker healthchecks
app.MapGet("/health", (IngestionStats stats) => Results.Json(new
{
    status = stats.MqttConnected ? "ok" : "mqtt_disconnected",
    stats.Received,
    stats.Accepted,
    stats.Rejected,
    stats.ProduceErrors,
    rejectReasons = stats.RejectReasons,
    stats.LastMessageAt
}, statusCode: stats.MqttConnected ? 200 : 503));

app.Run();
