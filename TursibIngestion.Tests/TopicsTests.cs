using Tursib.Contracts;
using Xunit;

namespace TursibIngestion.Tests;

public class TopicsTests
{
    [Fact]
    public void MqttTopicRoundTripsTheVehicleId()
    {
        var id = VehiclePosition.VehicleIdFor(7, 2);
        var topic = Topics.MqttPosition(7, id);

        Assert.Equal("tursib/vehicles/7/route_7_bus_2/position", topic);
        Assert.Equal(id, Topics.VehicleIdFromMqttTopic(topic));
    }

    [Fact]
    public void SerializedJsonUsesTheFieldNamesTheFrontendReads()
    {
        var json = System.Text.Encoding.UTF8.GetString(ContractJson.Serialize(new VehiclePosition
        {
            VehicleId = "route_7_bus_2",
            RouteId = 7,
            Latitude = 45.79,
            Longitude = 24.15,
            Timestamp = 1,
            NextStationEta = 42
        }));

        Assert.Contains("\"vehicleId\":\"route_7_bus_2\"", json);
        Assert.Contains("\"routeId\":7", json);
        Assert.Contains("\"latitude\":45.79", json);
        Assert.Contains("\"nextStationEta\":42", json);
        Assert.DoesNotContain("nextStationName", json); // nulls are omitted
    }
}
