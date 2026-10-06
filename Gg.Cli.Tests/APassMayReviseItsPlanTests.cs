using System.Text.Json;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The nomination tool offers the itinerary a pass is revising.
/// </summary>
/// <remarks>
/// <para>
/// <b>DECLARING IS HALF, and this class is only that half.</b>
/// <c>Gg.Runner.Tests.AnItineraryReachesTheNominationTests</c> is the other, and
/// it is the one that can fail on the real defect: slice fifty-seven recorded
/// `after` as proven on the strength of a test exactly like this one while every
/// fact the fleet shipped carried it null. Read both or neither.
/// </para>
/// <para>
/// <b>Why declared rather than left to the kind's prose.</b> `after` is a
/// constant, a contract member and an extractor read, and it is NOT a property
/// of this tool - an agent only passes it because the `plan` kind's instructions
/// name it. That works and it should not be the pattern: an argument an agent is
/// never offered is one it has to be told about twice, and the whole reason
/// revision was unreachable is that nobody could name an itinerary.
/// </para>
/// </remarks>
public class APassMayReviseItsPlanTests
{
    private const string List = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}";

    /// <remarks>
    /// <see cref="APassNominatesEachPieceOfWorkTests"/>'s helper, because the
    /// tool has to be read the way a client reads it rather than from a writer
    /// this test drives itself.
    /// </remarks>
    private static async Task<JsonElement> NominationAsync(bool several)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(List), output, sweep: false, several: several);

        using var listed = JsonDocument.Parse(output.ToString().Trim());

        return listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == NominationTool.Name)
            .Clone();
    }

    [Test]
    public async Task The_tool_offers_the_itinerary_a_pass_revises()
    {
        var properties = (await NominationAsync(several: true)).GetProperty("inputSchema").GetProperty("properties");

        await Assert.That(properties.TryGetProperty(NominationTool.Itinerary, out _)).IsTrue()
            .Because("everything behind revision was built and tested in slice fifty-three and "
                   + "none of it could be reached, because an agent was never offered a way to "
                   + "say which plan it was revising.");
    }

    [Test]
    public async Task It_is_offered_to_a_flight_that_may_open_only_one_too()
    {
        // BECAUSE JOINING IS NOT PROPOSING. An unplanned flight nominates under
        // an existing itinerary with no subject of its own, and it is an
        // ordinary flight rather than a pass with a cap - so gating this
        // argument on `several` would make a reflight unable to rejoin the plan
        // it belongs to.
        var properties = (await NominationAsync(several: false)).GetProperty("inputSchema").GetProperty("properties");

        await Assert.That(properties.TryGetProperty(NominationTool.Itinerary, out _)).IsTrue()
            .Because("a flight joining a plan it did not propose is the other half of this "
                   + "member, and it is never a flight that was told it may open several.");
    }

    [Test]
    public async Task It_is_never_required()
    {
        // ABSENT MEANS MINT, which is what a first plan wants and what every
        // nominator shipping today does. Required would refuse all of them.
        var required = (await NominationAsync(several: true)).GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(v => v.GetString()).ToList();

        await Assert.That(required).DoesNotContain(NominationTool.Itinerary)
            .Because("absent is the instruction to mint a new itinerary, so requiring it would "
                   + "make a first plan impossible to propose.");
    }

    [Test]
    public async Task The_argument_is_spelled_as_the_contract_spells_it()
    {
        await Assert.That(NominationTool.Itinerary).IsEqualTo("itinerary")
            .Because("two spellings of one argument is a nomination an agent writes and the "
                   + "platform drops.");
    }
}
