using Tursib.Contracts;

namespace TursibIngestion;

/// <summary>Outcome of validating one MQTT message.</summary>
public sealed record ValidationResult(VehiclePosition? Position, string? Reason)
{
    public bool IsValid => Position is not null && Reason is null;

    public static ValidationResult Ok(VehiclePosition position) => new(position, null);
    public static ValidationResult Reject(string reason, VehiclePosition? position = null) => new(position, reason);
}

/// <summary>
/// Checks that a device message is something the rest of the pipeline can trust.
/// Pure function: the caller passes the current time so tests are deterministic.
/// </summary>
public static class PositionValidator
{
    // Extent of the Tursib stops (45.65–45.88 N, 23.93–24.35 E) plus ~5 km margin.
    public const double MinLatitude = 45.60;
    public const double MaxLatitude = 45.93;
    public const double MinLongitude = 23.87;
    public const double MaxLongitude = 24.41;

    public const double MaxSpeedKmh = 120;
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromSeconds(30);

    public static ValidationResult Validate(string topic, ReadOnlySpan<byte> payload, DateTimeOffset now)
    {
        var topicVehicleId = Topics.VehicleIdFromMqttTopic(topic);
        if (topicVehicleId is null)
            return ValidationResult.Reject("unexpected_topic");

        var position = ContractJson.TryDeserialize(payload);
        if (position is null)
            return ValidationResult.Reject("malformed_json");

        if (position.SchemaVersion != VehiclePosition.CurrentSchemaVersion)
            return ValidationResult.Reject("unsupported_schema_version", position);

        if (position.VehicleId != topicVehicleId)
            return ValidationResult.Reject("vehicle_id_mismatch", position);

        if (position.RouteId <= 0)
            return ValidationResult.Reject("missing_route", position);

        if (!double.IsFinite(position.Latitude) || !double.IsFinite(position.Longitude)
            || position.Latitude is < MinLatitude or > MaxLatitude
            || position.Longitude is < MinLongitude or > MaxLongitude)
            return ValidationResult.Reject("out_of_service_area", position);

        if (!double.IsFinite(position.Speed) || position.Speed is < 0 or > MaxSpeedKmh)
            return ValidationResult.Reject("invalid_speed", position);

        if (!double.IsFinite(position.Heading) || position.Heading is < 0 or > 360)
            return ValidationResult.Reject("invalid_heading", position);

        var measuredAt = DateTimeOffset.FromUnixTimeSeconds(position.Timestamp);
        if (measuredAt < now - MaxAge)
            return ValidationResult.Reject("stale_timestamp", position);
        if (measuredAt > now + MaxClockSkew)
            return ValidationResult.Reject("future_timestamp", position);

        if (position.Occupancy is < 0 or > 100)
            return ValidationResult.Reject("invalid_occupancy", position);

        return ValidationResult.Ok(position);
    }
}
