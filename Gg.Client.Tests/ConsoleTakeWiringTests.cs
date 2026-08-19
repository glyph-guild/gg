namespace Gg.Client.Tests;

/// <summary>
/// The console builds a take session, so pressing the key does something.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the eleventh instance of <i>registered is not invoked</i>, and it is
/// why slice seven's step 0 was a step rather than a preamble.</b> Every piece of
/// the takeover existed and was tested - <c>TakeSeedComposer</c>,
/// <c>SeedPlacer</c>, <c>TakeoverReturnReader</c>, <c>HandoffRoot</c>,
/// <c>FlightTakenOver</c>, a declared endpoint - and none of it was reachable:
/// <c>ConsoleLoop</c> was constructed with both optional sessions defaulting to
/// null, so <c>Took</c> answered <i>"this console is not configured to take flights
/// over"</i> on every real invocation.
/// </para>
/// <para>
/// <b>Structural, because behaviour cannot see it.</b> The gap was not a wrong
/// answer; it was a constructor argument nobody passed, and every unit test passed
/// its own. A test that drove the console would have to drive a terminal.
/// </para>
/// </remarks>
public class ConsoleTakeWiringTests
{
    private static string Source(string project, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, project, file));
    }

    [Test]
    public async Task The_console_is_constructed_with_a_take_session()
    {
        // S7.4-02. `ConsoleLoop`'s take and hand arguments are optional so the UI
        // tests can leave them out; the product may not.
        var program = Source("Gg.Cli", "Program.cs");

        await Assert.That(program).Contains("new TakeSession")
            .Because("without this the key answers 'this console is not configured to take flights "
                   + "over', which is what it did for the whole of slices five and six.");
        // AND ITS TWIN IS STILL NOT PASSED, asserted as absent rather than left
        // ambiguous. HandSession needs an `infer` that spawns an agent to propose
        // what appears to have been done and an `ask` that reads a terminal
        // confirmation; the first means invoking an executor from the console,
        // which is a boundary slice seven does not touch.
        //
        // So HandedBack still answers "this console is not configured to hand
        // flights back". Asserting the absence is the honest version: a criterion
        // that quietly covered both would report a feature this console does not
        // have, which is the exact failure step 0 spent its time uncovering.
        await Assert.That(program).DoesNotContain("new HandSession")
            .Because("hand-back is unwired and named as such. When somebody wires it, this "
                   + "assertion fails and they flip it - which is what stops the gap being "
                   + "forgotten rather than closed.");
    }

    [Test]
    public async Task The_console_loads_the_principal_and_the_seed_it_needs_to_take_anything()
    {
        // The OTHER half of the same gap, and the one a constructor argument does
        // not fix. `Took` refuses when the selected row has no seed, so a console
        // holding a take session and no seed is still a console that cannot take.
        var start = Source("Gg.Console", "ConsoleStart.cs");

        await Assert.That(start).Contains("Principal")
            .Because("a takeover is an attributed act, and the record names the session - so the "
                   + "console has to know whose session it is before it offers the key.");
        await Assert.That(start).Contains("Seed")
            .Because("AppState.TakeSeed was assigned nowhere outside tests. Six occurrences in the "
                   + "repository, two of them assignments, both in TakeTests.");
    }

    [Test]
    public async Task The_scan_can_actually_fail()
    {
        // Liveness. Both assertions above pass on today's files and would pass just
        // as well against a file that had moved or a substring that never appears.
        await Assert.That(Source("Gg.Cli", "Program.cs")).Contains("ConsoleLoop");
        await Assert.That(Source("Gg.Cli", "Program.cs")).DoesNotContain("new NeverWrittenSession");
    }
}
