using System.Text.Json;
using System.Text.Json.Serialization;

namespace TursibMessaging;

/// <summary>
/// Contractul mesajelor de poziție: ce publică dispozitivul din autobuz pe MQTT și ce ajunge pe Kafka.
/// Aceleași câmpuri ca <c>BusPosition</c> din backend (un test din TursibBackend.Tests păzește compatibilitatea).
/// </summary>
public record VehiclePositionMessage
{
    /// <summary>Ex. route_12_bus_0. Dacă lipsește din payload se ia din topic.</summary>
    public string Id { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int RouteId { get; init; }
    public string? RouteNumber { get; init; }
    public string? RouteName { get; init; }
    /// <summary>Secunde Unix (UTC). Dacă lipsește (0), o completează serviciul de ingestie.</summary>
    public long Timestamp { get; init; }
    /// <summary>km/h</summary>
    public double Speed { get; init; }
    public double Heading { get; init; }
    public int? Occupancy { get; init; }
    public string? Status { get; init; }
    public int? DirectionId { get; init; }
    public int? NextStationId { get; init; }
    public string? NextStationName { get; init; }
    /// <summary>secunde</summary>
    public int? NextStationEta { get; init; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>Motivul pentru care un mesaj a fost respins (devine antet în DLQ și contor în metrici).</summary>
public enum RejectReason
{
    PayloadTooLarge,
    InvalidJson,
    InvalidTopic,
    IdMismatch,
    InvalidId,
    InvalidRoute,
    InvalidCoordinates,
    TimestampInFuture
}

public static class PositionParser
{
    public const int MaxPayloadBytes = 4096;
    public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Extrage id-ul vehiculului din topic: <c>tursib/vehicles/{id}/position</c>. Un dispozitiv poate publica doar
    /// pentru id-ul din topicul lui (ACL-ul brokerului leagă topicul de dispozitiv), deci payload-ul nu poate falsifica alt vehicul.
    /// </summary>
    public static string? VehicleIdFromTopic(string topic)
    {
        var parts = topic.Split('/');
        return parts is ["tursib", "vehicles", { Length: > 0 } id, "position"] ? id : null;
    }

    public static bool TryParse(string topic, ReadOnlySpan<byte> payload, DateTimeOffset now,
        out VehiclePositionMessage? message, out RejectReason reason)
    {
        message = null;
        if (payload.Length > MaxPayloadBytes) { reason = RejectReason.PayloadTooLarge; return false; }

        var vehicleId = VehicleIdFromTopic(topic);
        if (vehicleId == null) { reason = RejectReason.InvalidTopic; return false; }

        VehiclePositionMessage? parsed;
        try { parsed = JsonSerializer.Deserialize<VehiclePositionMessage>(payload, VehiclePositionMessage.Json); }
        catch (JsonException) { parsed = null; }
        if (parsed == null) { reason = RejectReason.InvalidJson; return false; }

        if (parsed.Id.Length > 0 && parsed.Id != vehicleId) { reason = RejectReason.IdMismatch; return false; }
        if (!vehicleId.StartsWith("route_", StringComparison.Ordinal) || vehicleId.Length > 64)
        { reason = RejectReason.InvalidId; return false; }
        if (parsed.RouteId <= 0) { reason = RejectReason.InvalidRoute; return false; }
        if (parsed.Latitude is < -90 or > 90 || parsed.Longitude is < -180 or > 180
            || double.IsNaN(parsed.Latitude) || double.IsNaN(parsed.Longitude)
            || (parsed.Latitude == 0 && parsed.Longitude == 0))
        { reason = RejectReason.InvalidCoordinates; return false; }

        var timestamp = parsed.Timestamp > 0 ? parsed.Timestamp : now.ToUnixTimeSeconds();
        if (timestamp > now.Add(MaxFutureSkew).ToUnixTimeSeconds()) { reason = RejectReason.TimestampInFuture; return false; }

        message = parsed with { Id = vehicleId, Timestamp = timestamp };
        reason = default;
        return true;
    }
}
