using TransitRealtime;
using ContractPosition = Tursib.Contracts.VehiclePosition;
using RtVehiclePosition = TransitRealtime.VehiclePosition;

namespace TursibBackend.Services.Events;

/// <summary>
/// Builds a GTFS-Realtime VehiclePositions feed (full dataset) from the live state.
/// Known limitation: the simulator does not know the real GTFS trip, so the
/// TripDescriptor carries route_id + direction_id only (trip matching is stage 3).
/// </summary>
public static class GtfsRealtimeFeedBuilder
{
    public static FeedMessage Build(IEnumerable<ContractPosition> positions, DateTimeOffset now)
    {
        var feed = new FeedMessage
        {
            Header = new FeedHeader
            {
                GtfsRealtimeVersion = "2.0",
                Incrementality = FeedHeader.Types.Incrementality.FullDataset,
                Timestamp = (ulong)now.ToUnixTimeSeconds()
            }
        };

        foreach (var p in positions.OrderBy(p => p.VehicleId, StringComparer.Ordinal))
            feed.Entity.Add(ToEntity(p));

        return feed;
    }

    public static FeedEntity ToEntity(ContractPosition p)
    {
        var trip = new TripDescriptor { RouteId = p.RouteId.ToString() };
        if (p.DirectionId is int direction and >= 0)
            trip.DirectionId = (uint)direction;

        var vehicle = new RtVehiclePosition
        {
            Trip = trip,
            Vehicle = new VehicleDescriptor { Id = p.VehicleId },
            Position = new Position
            {
                Latitude = (float)p.Latitude,
                Longitude = (float)p.Longitude,
                Bearing = (float)p.Heading,
                // GTFS-RT speed is in metres per second
                Speed = (float)(p.Speed / 3.6)
            },
            Timestamp = (ulong)Math.Max(0, p.Timestamp),
            CurrentStatus = p.Status switch
            {
                "at_station" => RtVehiclePosition.Types.VehicleStopStatus.StoppedAt,
                "approaching" => RtVehiclePosition.Types.VehicleStopStatus.IncomingAt,
                _ => RtVehiclePosition.Types.VehicleStopStatus.InTransitTo
            }
        };

        if (!string.IsNullOrEmpty(p.RouteNumber))
            vehicle.Vehicle.Label = p.RouteNumber;
        if (p.NextStationId is int stopId)
            vehicle.StopId = stopId.ToString();
        if (p.Occupancy is int occupancy and >= 0 and <= 100)
            vehicle.OccupancyPercentage = (uint)occupancy;

        return new FeedEntity { Id = p.VehicleId, Vehicle = vehicle };
    }
}
