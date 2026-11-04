using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tursib.Contracts;

/// <summary>
/// One position report from an on-board device (the simulator plays that role).
/// Same field names as the Firebase payload the frontend already parses, so the
/// JSON can be forwarded unchanged to the map.
/// </summary>
public sealed record VehiclePosition
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Stable vehicle id, e.g. "route_12_bus_0".</summary>
    public required string VehicleId { get; init; }

    public int RouteId { get; init; }
    public string? RouteNumber { get; init; }
    public string? RouteName { get; init; }

    public double Latitude { get; init; }
    public double Longitude { get; init; }

    /// <summary>Unix time in seconds when the device measured the position.</summary>
    public long Timestamp { get; init; }

    /// <summary>km/h</summary>
    public double Speed { get; init; }

    /// <summary>Degrees clockwise from north, 0–360.</summary>
    public double Heading { get; init; }

    /// <summary>Percent, 0–100.</summary>
    public int? Occupancy { get; init; }

    /// <summary>in_transit, approaching, at_station or departing.</summary>
    public string? Status { get; init; }

    public int? DirectionId { get; init; }

    public int? NextStationId { get; init; }
    public string? NextStationName { get; init; }

    /// <summary>Seconds until the next station.</summary>
    public int? NextStationEta { get; init; }

    public static string VehicleIdFor(int routeId, int busIndex) => $"route_{routeId}_bus_{busIndex}";
}

/// <summary>Topic names shared by every service in the pipeline.</summary>
public static class Topics
{
    /// <summary>MQTT topic filter the ingestion service subscribes to.</summary>
    public const string MqttPositionFilter = "tursib/vehicles/+/+/position";

    /// <summary>MQTT topic a device publishes its position on.</summary>
    public static string MqttPosition(int routeId, string vehicleId) => $"tursib/vehicles/{routeId}/{vehicleId}/position";

    /// <summary>
    /// Extracts the vehicle id from "tursib/vehicles/{routeId}/{vehicleId}/position";
    /// null when the topic has another shape.
    /// </summary>
    public static string? VehicleIdFromMqttTopic(string topic)
    {
        var parts = topic.Split('/');
        return parts.Length == 5 && parts[0] == "tursib" && parts[1] == "vehicles" && parts[4] == "position"
               && parts[3].Length > 0
            ? parts[3]
            : null;
    }

    /// <summary>Kafka topic with validated positions, keyed by vehicle id.</summary>
    public const string KafkaVehiclePositions = "vehicle-positions";

    /// <summary>Kafka topic with rejected messages; the reason is in the "reason" header.</summary>
    public const string KafkaVehiclePositionsDlq = "vehicle-positions-dlq";
}

/// <summary>JSON settings used on every hop (camelCase, nulls omitted).</summary>
public static class ContractJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] Serialize(VehiclePosition position) =>
        JsonSerializer.SerializeToUtf8Bytes(position, Options);

    /// <summary>Returns null instead of throwing on malformed JSON.</summary>
    public static VehiclePosition? TryDeserialize(ReadOnlySpan<byte> json)
    {
        try
        {
            return JsonSerializer.Deserialize<VehiclePosition>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
