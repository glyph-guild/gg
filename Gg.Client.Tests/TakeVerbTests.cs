using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Taking a flight over is a verb, so a headless machine and a second person's
/// terminal are the same path.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what makes the handoff portable rather than merely composed
/// elsewhere.</b> Slice seven's step 1 made the seed fetchable; without a verb the
/// only thing that could fetch one is a console, and a console needs a terminal.
/// A build machine, a second person's laptop and a script all take a flight over
/// through this.
/// </para>
/// <para>
/// <b>It writes nothing to a console, and that is asserted two ways.</b> A
/// <c>VerbResult</c> is the whole output, so <c>--json</c> can reproduce anything a
/// person sees - which is the property the console's panes depend on. The
/// structural half catches the easy regression; the behavioural half catches a
/// helper that writes from somewhere the scan does not look.
/// </para>
/// </remarks>
public class TakeVerbTests
{
    /// <summary>The verb's own source, for the structural half of S7.4-01.</summary>
    private static string CommandSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(
            dir!.FullName, "Gg.Client", "TakeCommands.cs"));
    }

    [Test]
    public async Task Taking_a_flight_yields_a_result_and_writes_nothing_to_a_console()
    {
        // S7.4-01, the structural half. A verb that printed would make `--json`
        // unable to reproduce what a person saw, and the console renders through
        // these same commands - so a write here lands in the middle of a TUI.
        var source = CommandSource();

        await Assert.That(source).DoesNotContain("Console.")
            .Because("the result IS the output. A verb that wrote would give the console a second "
                   + "voice it cannot render and --json a value it cannot carry.");
        await Assert.That(source).DoesNotContain("Write(")
            .Because("including a helper that wraps one.");
    }

    [Test]
    public async Task Taking_a_flight_claims_the_hold_before_it_reads_the_seed()
    {
        // ORDER IS THE PROPERTY. A verb that fetched the seed first and claimed
        // afterwards would hand somebody the work of a flight they are then told
        // they cannot have - and worse, would do it while a colleague was already
        // working it.
        await using var stub = new StubControlPlane();
        var commands = Commands(stub);

        var result = await commands.TakeAsync("GG-42");

        await Assert.That(result).IsTypeOf<VerbResult.Taken>();

        var paths = stub.ObservedPaths
            .Where(p => p.Contains("takeover:claim", StringComparison.Ordinal)
                     || p.EndsWith("/seed", StringComparison.Ordinal))
            .ToList();

        await Assert.That(paths.Count).IsEqualTo(2);
        await Assert.That(paths[0]).Contains("takeover:claim")
            .Because("the claim decides whether this person may have the flight at all, and it is "
                   + "the one call that has to happen first.");
        await Assert.That(paths[1]).EndsWith("/seed");
    }

    [Test]
    public async Task A_flight_somebody_else_holds_is_refused_and_the_refusal_names_them()
    {
        // The ordinary case, not an error path: two people looking at the same
        // stopped flight. What a person needs is who to ask.
        await using var stub = new StubControlPlane();
        stub.TakeoverHeldBy = new TakeoverHeld
        {
            By = "Ada",
            Since = new DateTimeOffset(2026, 8, 19, 9, 12, 0, TimeSpan.Zero),
            HeldUntil = new DateTimeOffset(2026, 8, 19, 9, 42, 0, TimeSpan.Zero),
        };

        var refusal = await Assert.ThrowsAsync<TakeoverRefusedException>(
            async () => await Commands(stub).TakeAsync("GG-42"));

        await Assert.That(refusal!.Message).Contains("Ada");
        await Assert.That(refusal.Message).Contains("09:12")
            .Because("'somebody else has this' sends a person nowhere. A name and an instant tell "
                   + "them whether to wait or come back tomorrow.");

        await Assert.That(stub.ObservedPaths.Any(p => p.EndsWith("/seed", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a refused claim must not hand over what the flight tried and ruled out. That "
                   + "is the work of a flight somebody else is holding.");
    }

    [Test]
    public async Task The_seed_a_person_reads_carries_no_machine()
    {
        // The whole point of the slice, asserted at the surface a person actually
        // meets rather than only on the wire type.
        await using var stub = new StubControlPlane();

        var result = (VerbResult.Taken)await Commands(stub).TakeAsync("GG-42");
        var rendered = VerbOutput.ToText(result);

        await Assert.That(rendered).Contains("GG-42");
        await Assert.That(rendered).DoesNotContain("/home/")
            .Because("a seed carrying a path on one machine is a seed that works on one machine.");
        await Assert.That(rendered).DoesNotContain(Environment.MachineName);
    }

    [Test]
    public async Task The_hold_is_reported_as_a_note_rather_than_inside_the_document()
    {
        // THE SAME ARRANGEMENT `envelope apply` USES, for the same reason. The
        // document is the seed; when the hold expires and how often to renew are
        // facts about this invocation, not about the flight - so they are notes,
        // and a result read back from JSON has none. Honest rather than lossy.
        await using var stub = new StubControlPlane();

        var result = (VerbResult.Taken)await Commands(stub).TakeAsync("GG-42");

        await Assert.That(result.Notes).IsNotEmpty();
        await Assert.That(string.Join(" ", result.Notes).ToLowerInvariant()).Contains("renew")
            .Because("a hold a person does not know to renew is a hold that lapses while they are "
                   + "working. Case-insensitive: the assertion is about the fact being said, not "
                   + "about where the sentence starts.");

        var roundTripped = VerbOutput.Parse(result.Kind, VerbOutput.ToJson(result));

        await Assert.That(((VerbResult.Taken)roundTripped).Notes).IsEmpty()
            .Because("the notes were never in the document, and inventing them on the way back "
                   + "would make a re-rendered payload claim a hold this process never took.");
        await Assert.That(((VerbResult.Taken)roundTripped).Value.FlightNumber)
            .IsEqualTo(result.Value.FlightNumber);
    }

    [Test]
    public async Task Handing_a_flight_back_claims_it_again_rather_than_guessing_a_generation()
    {
        // A SECOND INVOCATION HAS NO GENERATION, and there is nowhere honest to
        // keep one: a file on disk would be the machine-local state this slice
        // exists to remove. So the return re-claims, which is granted to the same
        // holder and refused to anybody else - and being refused here is exactly
        // right, because a decision recorded against somebody else's hold would
        // attribute their work to this person.
        await using var stub = new StubControlPlane();

        var result = await Commands(stub).ReturnAsync(
            "GG-42", TakeoverOutcomes.Completed, note: "rounding moved to the boundary");

        await Assert.That(result).IsTypeOf<VerbResult.Log>();

        var order = stub.ObservedPaths
            .Where(p => p.Contains("takeover:", StringComparison.Ordinal))
            .ToList();

        await Assert.That(order[0]).Contains("takeover:claim");
        await Assert.That(order[1]).Contains("takeover:return");
    }

    [Test]
    public async Task An_outcome_this_version_does_not_understand_never_reaches_the_wire()
    {
        // Refused HERE as well as there. Both sides fail closed on their own
        // format, which is the same arrangement the envelope has - and it means a
        // typo costs a diagnosis rather than a round trip.
        await using var stub = new StubControlPlane();

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await Commands(stub).ReturnAsync("GG-42", "probably-fine"));

        await Assert.That(stub.ObservedPaths.Any(
                p => p.Contains("takeover:return", StringComparison.Ordinal)))
            .IsFalse();
    }

    private static StoredSession ASession() => new()
    {
        SessionToken = "a-session",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        PrincipalDisplay = "edge",
        TenantId = "019ff8aa-1111-7000-8000-0000000000ff",
    };

    private static TakeCommands Commands(StubControlPlane stub) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(ASession()));
}
