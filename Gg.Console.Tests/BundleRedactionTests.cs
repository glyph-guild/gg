using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Nothing that crossed the live channel is ever in a bundle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asserted now, while it is cheap.</b> The live channel carries whatever a
/// runner printed - build output, a stack trace, whatever a tool decided to
/// echo - and today there is almost nothing in it. The version of this test
/// written after there IS something to leak is written by somebody who already
/// leaked it.
/// </para>
/// <para>
/// <b>The needle goes in through the real path.</b> Planted straight into a
/// bundle it would prove only that a string I put in one place is absent from
/// another. It goes into <c>AppState</c> the way runner output does, through
/// the reducer, and the bundle is built from the same state the console holds.
/// </para>
/// <para>
/// <b>And a liveness assertion.</b> Every absence here is satisfied by a
/// redactor that returned an empty document, so one test asserts the bundle
/// contains real content - not content the test handed it, but content it
/// went and got.
/// </para>
/// </remarks>
public class BundleRedactionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A string a bundle has no legitimate reason to carry.
    /// </summary>
    /// <remarks>
    /// Shaped like the thing that would actually leak: a token echoed by a
    /// tool into its own output. Distinctive enough that finding it anywhere
    /// is unambiguous.
    /// </remarks>
    private const string Needle = "ghp_liveChannelNeedle3QY7bB1kZr";

    internal static EnvironmentIdentity AnEnvironment() => new()
    {
        HostFingerprint = "b8c1f0a9",
        Locks = [],
        Tools = [new ToolVersion { Name = "dotnet", Version = "10.0.0" }],
        Provenance = EnvironmentProvenance.Fresh,
    };

    internal static DoctorReport AReport() => new()
    {
        Checks =
        [
            new DoctorCheck
            {
                Name = DoctorChecks.ControlPlane,
                Passed = true,
                Detail = "reachable at https://good-grief.test/",
                Blocking = true,
                Fixable = false,
            },
        ],
    };

    /// <summary>State with the needle in the live channel, put there the real way.</summary>
    private static AppState WithNeedleOnTheWire()
    {
        var state = new AppState { LiveVisible = true };

        foreach (var line in (string[])
                 ["running build", $"warning: token {Needle} was printed by a tool", "done"])
        {
            state = Reducer.StreamArrived(state, new StreamLine
            {
                Kind = StreamLineKind.Raw,
                Text = line,
                At = T0,
            });
        }

        return state;
    }

    /// <summary>
    /// The same rule, against lines a real agent produced.
    /// </summary>
    /// <remarks>
    /// <b>Re-verified now that the channel has something in it.</b> The
    /// assertions above were written while the live channel was empty, which was
    /// the honest moment to write them and a weak moment to trust them: every
    /// one would have passed against a reducer that dropped everything. These
    /// go through the transport a runner actually uses - a file the console
    /// tails - so what is asserted absent is what really travelled.
    /// </remarks>
    private static AppState WithARealStream(out string needleLine)
    {
        var path = Path.Combine(
            Path.GetTempPath(), "gg-bundle-" + Guid.NewGuid().ToString("N")[..8], "flight.ndjson");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Shaped exactly like what the executor writes for a real run, with the
        // needle where one really would be: echoed by a tool into its result.
        needleLine = $"→ ok  export GH_TOKEN={Needle}";
        File.WriteAllLines(path,
        [
            """{"kind":"setup","text":"session init","at":"2026-08-13T12:00:00+00:00"}""",
            """{"kind":"text","text":"I'll look at the project's style first.","at":"2026-08-13T12:00:00+00:00"}""",
            """{"kind":"tool","text":"Read","at":"2026-08-13T12:00:00+00:00"}""",
            $$"""{"kind":"tool","text":"{{needleLine}}","at":"2026-08-13T12:00:00+00:00"}""",
            """{"kind":"meta","text":"loop success","at":"2026-08-13T12:00:00+00:00"}""",
        ]);

        var state = new AppState { LiveVisible = true };
        foreach (var line in new LiveTail(path).Read())
        {
            state = Reducer.StreamArrived(state, line);
        }

        return state;
    }

    [Test]
    public async Task A_real_stream_reaches_the_console_and_none_of_it_reaches_a_bundle()
    {
        // FOUR SINKS, because a rule that holds in three places and not the
        // fourth is not a rule. The json a machine reads, the text a person
        // pastes, the state that is serialized when the terminal is released,
        // and the held buffer that freeze fills.
        var state = WithARealStream(out var needleLine);

        await Assert.That(state.Live.Any(l => l.Text.Contains(Needle, StringComparison.Ordinal)))
            .IsTrue()
            .Because("the plant has to have worked, or every absence below is vacuous.");
        await Assert.That(state.Live.Count).IsEqualTo(5)
            .Because("all five lines arrived through the real transport.");

        var bundle = ConsoleData.BundleFrom(state, T0, AnEnvironment(), AReport(), flightLog: null);

        var json = VerbOutput.ToJson(new VerbResult.Bundle(bundle));
        var text = VerbOutput.ToText(new VerbResult.Bundle(bundle));
        var frozen = Reducer.Reduce(state, Command.ToggleFreeze);
        var held = VerbOutput.ToJson(new VerbResult.Bundle(
            ConsoleData.BundleFrom(
                Reducer.StreamArrived(frozen, new StreamLine
                {
                    Kind = StreamLineKind.Tool,
                    Text = needleLine,
                    At = T0,
                }),
                T0, AnEnvironment(), AReport(), flightLog: null)));

        var state_json = System.Text.Json.JsonSerializer.Serialize(
            state, AppStateJsonContext.Default.AppState);

        foreach (var (sink, content) in ((string, string)[])
                 [("json", json), ("text", text), ("held", held), ("serialized state", state_json)])
        {
            if (sink == "serialized state")
            {
                // The one that is SUPPOSED to carry it: releasing the terminal
                // and rebuilding views from surviving state is the architecture,
                // and the live pane has to survive that. It is a local file that
                // never leaves the machine - which is exactly why the bundle,
                // which does leave, is built from a redaction rather than from
                // this.
                await Assert.That(content).Contains(Needle)
                    .Because("state survives terminal release, and this proves the two documents "
                           + "are genuinely different rather than the same one twice.");
                continue;
            }

            await Assert.That(content).DoesNotContain(Needle)
                .Because($"the live channel reached the {sink} sink.");
            await Assert.That(content).DoesNotContain("session init")
                .Because($"and no other line of it belongs in {sink} either.");
        }
    }

    [Test]
    public async Task The_needle_really_is_on_the_wire()
    {
        // Proof the plant worked. Without this the absence below could pass
        // because the reducer dropped the line, which is a different bug and
        // would hide this one forever.
        var state = WithNeedleOnTheWire();

        await Assert.That(state.Live.Any(l => l.Text.Contains(Needle, StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task No_line_of_the_live_channel_reaches_the_bundle()
    {
        var state = WithNeedleOnTheWire();

        var bundle = ConsoleData.BundleFrom(state, T0, AnEnvironment(), AReport(), flightLog: null);
        var json = VerbOutput.ToJson(new VerbResult.Bundle(bundle));

        await Assert.That(json).DoesNotContain(Needle);
        await Assert.That(json).DoesNotContain("running build")
            .Because("the needle is the interesting line, but no line of it belongs here.");
    }

    [Test]
    public async Task The_rendered_bundle_does_not_carry_it_either()
    {
        // --json and the human rendering are two surfaces and the rule is
        // about both. A redactor that only cleaned the document a machine
        // reads would leak into the one a person pastes into a chat.
        var state = WithNeedleOnTheWire();

        var rendered = VerbOutput.ToText(new VerbResult.Bundle(
            ConsoleData.BundleFrom(state, T0, AnEnvironment(), AReport(), flightLog: null)));

        await Assert.That(rendered).DoesNotContain(Needle);
    }

    [Test]
    public async Task The_bundle_it_produced_was_a_real_one()
    {
        // The liveness assertion. Everything above is satisfied by a function
        // that returns an empty document, and an empty bundle is worse than no
        // bundle - it is a support request that looks answered.
        var state = WithNeedleOnTheWire();

        var bundle = ConsoleData.BundleFrom(state, T0, AnEnvironment(), AReport(), flightLog: null);
        var json = VerbOutput.ToJson(new VerbResult.Bundle(bundle));

        await Assert.That(bundle.Binary).IsEqualTo(GgVersions.Binary);
        await Assert.That(bundle.Environment.HostFingerprint).IsEqualTo("b8c1f0a9");
        await Assert.That(bundle.Checks).IsNotEmpty();
        await Assert.That(json).Contains("good-grief.test")
            .Because("it went and got the connectivity result rather than returning a shell.");
        await Assert.That(json.Length).IsGreaterThan(200);
    }

    [Test]
    public async Task Held_lines_are_no_more_exportable_than_live_ones()
    {
        // A freeze moves lines from Live to Held. A redactor that only knew
        // about one of the two would leak everything that arrived while
        // somebody was reading.
        var state = WithNeedleOnTheWire() with { Frozen = true };
        state = Reducer.StreamArrived(state, new StreamLine
        {
            Kind = StreamLineKind.Raw,
            Text = $"held: {Needle}",
            At = T0,
        });

        await Assert.That(state.Held.Any(l => l.Text.Contains(Needle, StringComparison.Ordinal))).IsTrue()
            .Because("if the freeze did not hold it, this test is asserting nothing.");

        var json = VerbOutput.ToJson(new VerbResult.Bundle(
            ConsoleData.BundleFrom(state, T0, AnEnvironment(), AReport(), flightLog: null)));

        await Assert.That(json).DoesNotContain(Needle);
    }

    [Test]
    public async Task A_work_item_title_does_not_reach_the_bundle_either()
    {
        // THE SAME ARGUMENT ONE PANE OVER. A browse listing holds work item
        // titles because choosing without them is choosing by number, and a
        // title is customer content by any reading. It is safe for the reason
        // everything here is safe: BundleFrom takes the whole state and reads
        // almost none of it, so the needle is in scope and still does not come
        // out. Asserted rather than assumed, because "reads almost none of it"
        // is a sentence that stops being true one convenient property at a time.
        const string Needle = "ACME-CONFIDENTIAL-ROADMAP-ITEM";

        var state = new AppState
        {
            Browse = new BrowseListing
            {
                ProviderKey = "a-tracker",
                Items =
                [
                    new BrowseRow
                    {
                        Id = "18398",
                        Title = Needle,
                        State = "Active",
                        Updated = "2026-09-05T01:06:13Z",
                    },
                ],
            },
        };

        await Assert.That(state.Browse!.Items[0].Title).IsEqualTo(Needle)
            .Because("the plant has to have worked, or the absence below is vacuous.");

        var bundle = ConsoleData.BundleFrom(state, T0, AnEnvironment(), AReport(), flightLog: null);

        await Assert.That(VerbOutput.ToJson(new VerbResult.Bundle(bundle))).DoesNotContain(Needle);
        await Assert.That(VerbOutput.ToText(new VerbResult.Bundle(bundle))).DoesNotContain(Needle)
            .Because("a bundle is a thing a person sends us, in both renderings.");
    }
}
