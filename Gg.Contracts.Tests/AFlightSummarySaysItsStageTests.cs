using System.Text.Json;

namespace Gg.Contracts.Tests;

/// <summary>
/// A flight summary carries the stage its flight reached - created, ready, leased, worked,
/// evaluated, ended - so a list can say where each flight is without reading every story (owner,
/// 2026-10-09: the flights tab should show the stage).
/// </summary>
public class AFlightSummarySaysItsStageTests
{
    [Test]
    public async Task The_stage_is_on_the_wire_and_absent_reads_as_unsaid()
    {
        var json = """{"flightId":"x","flightNumber":"GG-1031","name":"n","intent":{"kind":"text","text":"t"},"createdAt":"2026-10-09T04:42:39Z","runnerProtocolVersion":1,"factVocabularyVersion":"0.1.0","constitutionVersion":"1.0.0","envelopeVersion":"v18","attempts":1,"facts":[],"stage":"leased"}""";

        var read = JsonSerializer.Deserialize<FlightSummary>(json, JsonSerializerOptions.Web)!;
        await Assert.That(read.Stage).IsEqualTo(FlightStages.Leased);

        var older = JsonSerializer.Deserialize<FlightSummary>(json.Replace(",\"stage\":\"leased\"", ""), JsonSerializerOptions.Web)!;
        await Assert.That(older.Stage).IsNull()
            .Because("a control plane that does not send it yet says nothing, and nothing is invented.");
    }
}
