using System.Text.Json;

namespace Gg.Contracts.Tests;

public class ProtocolHelloTests
{
    private static readonly Bogus.Faker Faker = new();

    [Test]
    public async Task RoundTripsThroughCamelCaseJson()
    {
        var hello = new ProtocolHello
        {
            ProtocolVersion = Faker.Random.Int(min: 1),
            Component = Faker.PickRandom("console", "runner", "control-plane"),
            ComponentVersion = Faker.System.Semver(),
        };

        var json = JsonSerializer.Serialize(hello, JsonSerializerOptions.Web);
        var back = JsonSerializer.Deserialize<ProtocolHello>(json, JsonSerializerOptions.Web);

        await Assert.That(back).IsEqualTo(hello);
        await Assert.That(json).Contains("protocolVersion");
    }
}
