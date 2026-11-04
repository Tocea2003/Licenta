using System.Text;
using Tursib.Contracts;
using TursibIngestion;
using Xunit;

namespace TursibIngestion.Tests;

public class PositionValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
    private const string Topic = "tursib/vehicles/12/route_12_bus_0/position";

    private static VehiclePosition Valid() => new()
    {
        VehicleId = "route_12_bus_0",
        RouteId = 12,
        RouteNumber = "12",
        Latitude = 45.7983,
        Longitude = 24.1256,
        Timestamp = Now.ToUnixTimeSeconds() - 1,
        Speed = 32.5,
        Heading = 90,
        Occupancy = 40,
        Status = "in_transit"
    };

    private static ValidationResult Check(VehiclePosition p, string topic = Topic) =>
        PositionValidator.Validate(topic, ContractJson.Serialize(p), Now);

    [Fact]
    public void AcceptsAWellFormedPosition()
    {
        var result = Check(Valid());

        Assert.True(result.IsValid);
        Assert.Equal("route_12_bus_0", result.Position!.VehicleId);
    }

    [Fact]
    public void RejectsMalformedJson()
    {
        var result = PositionValidator.Validate(Topic, Encoding.UTF8.GetBytes("{not json"), Now);
        Assert.Equal("malformed_json", result.Reason);
    }

    [Fact]
    public void RejectsJsonWithoutVehicleId()
    {
        var result = PositionValidator.Validate(Topic, Encoding.UTF8.GetBytes("{\"routeId\":12}"), Now);
        Assert.Equal("malformed_json", result.Reason);
    }

    [Theory]
    [InlineData("tursib/vehicles/12/position")]
    [InlineData("other/vehicles/12/route_12_bus_0/position")]
    [InlineData("tursib/vehicles/12/route_12_bus_0/status")]
    public void RejectsUnexpectedTopics(string topic)
    {
        Assert.Equal("unexpected_topic", Check(Valid(), topic).Reason);
    }

    [Fact]
    public void RejectsWhenTopicAndPayloadDisagreeOnVehicle()
    {
        var result = Check(Valid(), "tursib/vehicles/12/route_12_bus_1/position");
        Assert.Equal("vehicle_id_mismatch", result.Reason);
    }

    [Fact]
    public void RejectsUnknownSchemaVersion()
    {
        Assert.Equal("unsupported_schema_version", Check(Valid() with { SchemaVersion = 2 }).Reason);
    }

    [Theory]
    [InlineData(44.43, 26.10)]   // Bucharest
    [InlineData(0, 0)]
    [InlineData(45.79, 25.00)]
    public void RejectsPositionsOutsideTheServiceArea(double lat, double lon)
    {
        var result = Check(Valid() with { Latitude = lat, Longitude = lon });
        Assert.Equal("out_of_service_area", result.Reason);
    }

    [Fact]
    public void AcceptsTheOutermostStops()
    {
        // Cisnădie (south) and the eastern end of the network are inside the box
        Assert.True(Check(Valid() with { Latitude = 45.6515, Longitude = 24.1500 }).IsValid);
        Assert.True(Check(Valid() with { Latitude = 45.8000, Longitude = 24.3545 }).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(150)]
    [InlineData(120.5)]
    public void RejectsImpossibleSpeeds(double speed)
    {
        Assert.Equal("invalid_speed", Check(Valid() with { Speed = speed }).Reason);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(400)]
    public void RejectsInvalidHeadings(double heading)
    {
        Assert.Equal("invalid_heading", Check(Valid() with { Heading = heading }).Reason);
    }

    [Fact]
    public void RejectsStaleAndFutureTimestamps()
    {
        Assert.Equal("stale_timestamp",
            Check(Valid() with { Timestamp = Now.AddMinutes(-6).ToUnixTimeSeconds() }).Reason);
        Assert.Equal("future_timestamp",
            Check(Valid() with { Timestamp = Now.AddMinutes(1).ToUnixTimeSeconds() }).Reason);
    }

    [Fact]
    public void ToleratesSmallClockSkew()
    {
        Assert.True(Check(Valid() with { Timestamp = Now.AddSeconds(20).ToUnixTimeSeconds() }).IsValid);
    }

    [Fact]
    public void RejectsMissingRouteAndInvalidOccupancy()
    {
        Assert.Equal("missing_route", Check(Valid() with { RouteId = 0 }).Reason);
        Assert.Equal("invalid_occupancy", Check(Valid() with { Occupancy = 140 }).Reason);
    }
}
