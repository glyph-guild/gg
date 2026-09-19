using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// A widening the apply diverted rides a flight, and that flight is looked for
/// like any other the console opened.
/// </summary>
/// <remarks>
/// <b>The gate is there at once and the flight is not.</b> A diverted document
/// or a declaration that gated opens its gate in the request - so the gate list
/// is right straight away - and names a flight the Flight context has not
/// projected yet. The re-read after applying still runs, because the documents'
/// versions moved; the flight it rides is watched for beside it.
/// </remarks>
public class AFlightAnApplyDivertedToIsWatchedForTests
{
    [Test]
    public async Task Every_flight_the_apply_diverted_to_is_looked_for()
    {
        var applied = new VerbResult.AirspaceApplied(new EstateApplied
        {
            Applied =
            [
                new AppliedDocument
                {
                    Name = "root",
                    Path = "envelopes/root.yaml",
                    Version = "v9",
                    Changed = false,
                    Widens = "moves",
                    Flight = "f-widen",
                    Awaiting = "a-lead",
                },
                new AppliedDocument
                {
                    Name = "team",
                    Path = "envelopes/team.yaml",
                    Version = "v3",
                    Changed = true,
                },
            ],
            Retiring = [],
            Declared =
            [
                new NameDeclared
                {
                    Name = "payments",
                    Role = "team",
                    Parent = "root",
                    Flight = "f-declare",
                    Awaiting = "a-lead",
                },
            ],
        });

        var after = ConsoleApply.Watching(new AppState(), applied);

        await Assert.That(after.Expecting.Select(e => (e.Kind, e.Id))).IsEquivalentTo(new[]
        {
            (ExpectationKind.FlightAppears, "f-widen"),
            (ExpectationKind.FlightAppears, "f-declare"),
        })
            .Because("each gated change rides a flight nobody can see yet, and a document "
                   + "applied outright rides none.");
    }

    [Test]
    public async Task Nothing_applied_is_nothing_to_look_for()
    {
        var after = ConsoleApply.Watching(new AppState(), null);

        await Assert.That(after.Expecting).IsEmpty();
    }
}
