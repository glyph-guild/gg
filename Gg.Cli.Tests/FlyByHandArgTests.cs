namespace Gg.Cli.Tests;

/// <summary>
/// A person can say they are flying this one themselves, on every line
/// <c>gg fly</c> already takes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A flag rather than a verb, and that is rule 1 in the argument list.</b> A
/// hand-flown flight is created, governed, gated and landed identically — the
/// only differences are which executor runs and how the lease is obtained — so
/// a separate verb would be a second way to open a flight, with its own subset
/// of the flags and its own drift. <c>gg fly --hand</c> is <c>gg fly</c>.
/// </para>
/// <para>
/// <b>Position-independent, the way <c>--json</c> and <c>--all</c> already
/// are.</b> A person types a trailing flag in both places and being told off for
/// one of them is not helpful. Stripped before the verb is matched, for the
/// same reason those two are: an option left in the list is mistaken for a verb.
/// </para>
/// <para>
/// <b>But refused on anything that is not <c>fly</c>.</b> Stripping it globally
/// would accept <c>gg flights --hand</c> and do nothing, which is the shape
/// <c>IEmitsResult</c> exists to stop for <c>--json</c> — a flag that reads as
/// an instruction and is not one.
/// </para>
/// </remarks>
public class FlyByHandArgTests
{
    private static CliAction.Fly Flown(params string[] args)
    {
        var action = CliArgs.Parse(args);

        return action as CliAction.Fly ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as a flight but as "
          + $"{action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    // ---- S26.4-01 ----

    [Test]
    public async Task Flying_by_hand_is_the_same_flight_with_a_flag_on_it()
    {
        var action = Flown("fly", "fix the timeout", "--hand");

        await Assert.That(action.Text).IsEqualTo("fix the timeout");
        await Assert.That(action.ByHand).IsTrue();
    }

    [Test]
    public async Task An_ordinary_flight_is_not_flown_by_hand()
    {
        // THE POISON TWIN. A parser that set ByHand unconditionally would
        // satisfy every row above it.
        await Assert.That(Flown("fly", "fix the timeout").ByHand).IsFalse();
    }

    // ---- S26.4-02 ----

    [Test]
    public async Task It_composes_with_the_repository_a_flight_names()
    {
        var action = Flown("fly", "--ticket", "tracker#26", "--repo", "payments", "--hand");

        await Assert.That(action.Provider).IsEqualTo("tracker");
        await Assert.That(action.Id).IsEqualTo("26");
        await Assert.That(action.Repository).IsEqualTo("payments");
        await Assert.That(action.ByHand).IsTrue();
    }

    [Test]
    public async Task It_composes_with_a_uri()
    {
        var action = Flown("fly", "--uri", "https://forge.example/acme/w/issues/1", "--hand");

        await Assert.That(action.Uri).IsEqualTo("https://forge.example/acme/w/issues/1");
        await Assert.That(action.ByHand).IsTrue();
    }

    [Test]
    public async Task It_can_be_typed_before_the_intent()
    {
        // POSITION-INDEPENDENT, and this is the position a person actually
        // reaches for when they decide halfway through the line.
        await Assert.That(Flown("fly", "--hand", "fix the timeout").ByHand).IsTrue();
    }

    [Test]
    public async Task It_composes_with_json()
    {
        var action = Flown("fly", "fix the timeout", "--hand", "--json");

        await Assert.That(action.ByHand).IsTrue();
        await Assert.That(action.Json).IsTrue();
    }

    // ---- the flag that would do nothing ----

    [Test]
    public async Task Asking_to_hand_fly_something_that_is_not_a_flight_is_refused()
    {
        // NOT SILENTLY STRIPPED. `gg flights --hand` reads as an instruction and
        // is not one, and a person who typed it believes something happened.
        // This is what IEmitsResult does for --json, done for one verb rather
        // than by a type.
        var action = CliArgs.Parse(["flights", "--hand"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>();

        var diagnosis = ((CliAction.Unknown)action).Message;

        await Assert.That(diagnosis).Contains("--hand");
        await Assert.That(diagnosis).Contains("fly")
            .Because("a refusal that does not name the verb it belongs to leaves somebody "
                   + "guessing which line to retype.");
    }

    // ---- S26.4-03 ----

    [Test]
    public async Task A_trailing_hand_with_nothing_to_fly_is_refused_by_name()
    {
        // `gg fly --hand` alone is somebody who meant to say what to work on.
        // Falling through to the free-text arm would open a real flight called
        // nothing at all - and `fly` is the one verb whose side effect a person
        // cannot undo from here.
        var action = CliArgs.Parse(["fly", "--hand"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)action).Message).Contains("gg fly");
    }
}
