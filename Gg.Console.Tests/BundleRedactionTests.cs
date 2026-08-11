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

    private static EnvironmentIdentity AnEnvironment() => new()
    {
        HostFingerprint = "b8c1f0a9",
        Locks = [],
        Tools = [new ToolVersion { Name = "dotnet", Version = "10.0.0" }],
        Provenance = EnvironmentProvenance.Fresh,
    };

    private static DoctorReport AReport() => new()
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
}
