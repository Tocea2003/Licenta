using TransitRealtime;

namespace TursibBackend.Services;

/// <summary>Construiește feed-ul GTFS-Realtime „VehiclePositions” din pozițiile live.</summary>
public static class GtfsRealtimeFeedBuilder
{
    public const string Version = "2.0";

    /// <param name="maxAgeSeconds">pozițiile mai vechi nu intră în feed (un vehicul care nu mai raportează dispare)</param>
    public static FeedMessage Build(IEnumerable<BusPosition> positions, DateTimeOffset now, int maxAgeSeconds = 120)
    {
        var oldest = now.ToUnixTimeSeconds() - maxAgeSeconds;
        var feed = new FeedMessage
        {
            Header = new FeedHeader
            {
                GtfsRealtimeVersion = Version,
                Incrementality = FeedHeader.Types.Incrementality.FullDataset,
                Timestamp = (ulong)now.ToUnixTimeSeconds()
            }
        };

        foreach (var p in positions.Where(p => p.Timestamp >= oldest).OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var vehicle = new VehiclePosition
            {
                // Nu avem trip_id (simulatorul nu urmează o cursă concretă): GTFS-RT permite UNSCHEDULED cu route_id.
                Trip = new TripDescriptor
                {
                    RouteId = p.RouteId.ToString(),
                    ScheduleRelationship = TripDescriptor.Types.ScheduleRelationship.Unscheduled
                },
                Vehicle = new VehicleDescriptor { Id = p.Id, Label = p.RouteNumber is { Length: > 0 } n ? $"{n} · {p.Id}" : p.Id },
                Position = new Position
                {
                    Latitude = (float)p.Latitude,
                    Longitude = (float)p.Longitude,
                    Bearing = (float)p.Heading,
                    Speed = (float)(p.Speed / 3.6)          // GTFS-RT: m/s
                },
                Timestamp = (ulong)p.Timestamp,
                CurrentStatus = StopStatus(p.Status)
            };
            if (p.DirectionId is { } d) vehicle.Trip.DirectionId = (uint)d;
            if (p.NextStationId is { } stop) vehicle.StopId = stop.ToString();
            if (p.Occupancy is { } occupancy) vehicle.OccupancyStatus = Occupancy(occupancy);

            feed.Entity.Add(new FeedEntity { Id = p.Id, Vehicle = vehicle });
        }
        return feed;
    }

    public static VehiclePosition.Types.VehicleStopStatus StopStatus(string? status) => status switch
    {
        "at_station" => VehiclePosition.Types.VehicleStopStatus.StoppedAt,
        "approaching" => VehiclePosition.Types.VehicleStopStatus.IncomingAt,
        _ => VehiclePosition.Types.VehicleStopStatus.InTransitTo
    };

    /// <summary>Procent de ocupare (0-100) -> categoria GTFS-RT.</summary>
    public static VehiclePosition.Types.OccupancyStatus Occupancy(int percent) => percent switch
    {
        < 10 => VehiclePosition.Types.OccupancyStatus.Empty,
        < 40 => VehiclePosition.Types.OccupancyStatus.ManySeatsAvailable,
        < 65 => VehiclePosition.Types.OccupancyStatus.FewSeatsAvailable,
        < 85 => VehiclePosition.Types.OccupancyStatus.StandingRoomOnly,
        < 95 => VehiclePosition.Types.OccupancyStatus.CrushedStandingRoomOnly,
        _ => VehiclePosition.Types.OccupancyStatus.Full
    };
}
