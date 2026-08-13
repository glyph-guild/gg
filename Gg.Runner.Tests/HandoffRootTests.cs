using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight that did not land keeps its tree, without teaching the sweep
/// anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>The flights worth taking over are the ones that had nothing left to
/// take.</b> A person takes over because the agent got stuck, so the interesting
/// case is violated or exhausted - and in that case there is no branch and the
/// work exists only in the working tree, which step 3 threw away deliberately.
/// </para>
/// <para>
/// <b>So the tree moves rather than the sweep learning.</b> The startup sweep
/// works because a runner that is starting holds no lease, so every tree under
/// the working root belongs to a process that is gone: "all of them", a rule with
/// no state behind it, which therefore cannot be wrong about which trees are
/// live. A sweep that had to know which trees were takeable would lose exactly
/// that, and it is the property worth more than the convenience.
/// </para>
/// </remarks>
public class HandoffRootTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static string ATreeAt(string path, string content = "print('hello')\n")
    {
        Directory.CreateDirectory(Path.Combine(path, "src"));
        File.WriteAllText(Path.Combine(path, "src", "greet.py"), content);

        return path;
    }

    [Test]
    public async Task A_held_tree_leaves_the_working_root_entirely()
    {
        // The move is the mechanism. A tree that stayed under the working root
        // and was merely marked would make the sweep's rule wrong.
        using var trees = new ScratchTreeRoot();

        var working = trees.Root.For("flight-1");
        ATreeAt(working);

        var held = trees.Handoff.Hold("flight-1", working);

        await Assert.That(held).IsNotNull();
        await Assert.That(Directory.Exists(working)).IsFalse()
            .Because("it moved. A copy would double the disk this root exists to spend "
                   + "deliberately.");
        await Assert.That(File.Exists(Path.Combine(held!.Path, "src", "greet.py"))).IsTrue()
            .Because("and the work is all still there, which is the point of keeping it.");
    }

    [Test]
    public async Task The_startup_sweep_still_deletes_everything_it_finds()
    {
        // UNCHANGED, and asserted here rather than assumed: the sweep's rule is
        // still "all of them" and it still has no state behind it.
        using var trees = new ScratchTreeRoot();

        ATreeAt(trees.Root.For("flight-1"));
        ATreeAt(trees.Root.For("flight-2"));
        trees.Handoff.Hold("flight-3", ATreeAt(trees.Root.For("flight-3")));

        var swept = trees.Root.SweepOrphans();

        await Assert.That(swept).IsEqualTo(2);
        await Assert.That(Directory.EnumerateDirectories(trees.Root.Path)).IsEmpty();
        await Assert.That(trees.Handoff.Held().Count).IsEqualTo(1)
            .Because("the sweep never looks at the handoff root, so a held tree survives a runner "
                   + "restart - which is the only way a takeover is possible at all.");
    }

    [Test]
    public async Task The_size_of_a_held_tree_is_measured_when_it_is_kept()
    {
        // Disk is the first resource this product spends in a customer's
        // environment, and it had no number against it. A retention policy chosen
        // without one is a guess.
        using var trees = new ScratchTreeRoot();

        var held = trees.Handoff.Hold(
            "flight-1", ATreeAt(trees.Root.For("flight-1"), new string('x', 4096)));

        await Assert.That(held!.Bytes).IsGreaterThanOrEqualTo(4096);
    }

    [Test]
    public async Task Retention_is_a_number_somebody_chose()
    {
        // Not a default that happens. One constant, in one place, so changing it
        // is a decision rather than an emergent property of when somebody last
        // ran something.
        await Assert.That(HandoffRoot.Retention).IsEqualTo(TimeSpan.FromDays(7));
    }

    [Test]
    public async Task Expiry_deletes_what_has_outlived_retention_and_says_what_it_deleted()
    {
        // Returned rather than logged. Expiring somebody's only copy of an
        // agent's work with no line anywhere saying why is the silent
        // degradation this project keeps naming.
        var clock = new FakeTime(T0);
        using var trees = new ScratchTreeRoot();
        var handoff = new HandoffRoot(Path.Combine(trees.Root.Path, "..", "held"), clock);

        try
        {
            handoff.Hold("flight-old", ATreeAt(trees.Root.For("flight-old")));

            clock.Advance(HandoffRoot.Retention + TimeSpan.FromHours(1));

            handoff.Hold("flight-new", ATreeAt(trees.Root.For("flight-new")));

            var expired = handoff.Expire();

            await Assert.That(expired.Count).IsEqualTo(1);
            await Assert.That(expired.Single().FlightId).IsEqualTo("flight-old")
                .Because("named, so a flight that was takeable on Monday and is not on Tuesday has "
                       + "a line somewhere saying why.");
            await Assert.That(expired.Single().Bytes).IsGreaterThan(0)
                .Because("and how much disk came back, which is the number nobody had.");

            await Assert.That(handoff.Held().Single().FlightId).IsEqualTo("flight-new");
        }
        finally
        {
            if (Directory.Exists(handoff.Path))
            {
                Directory.Delete(handoff.Path, recursive: true);
            }
        }
    }

    [Test]
    public async Task Holding_a_flight_that_left_nothing_behind_is_not_an_error()
    {
        // A flight killed before it materialized anything has no tree. That is
        // ordinary, and throwing would turn it into a runner that will not start
        // its next flight.
        using var trees = new ScratchTreeRoot();

        await Assert.That(trees.Handoff.Hold("flight-1", trees.Root.For("flight-1"))).IsNull();
    }

    [Test]
    public async Task Holding_twice_replaces_rather_than_failing()
    {
        // A run that died between the move and the delete leaves one behind.
        // Refusing would make the next flight with that id untakeable forever.
        using var trees = new ScratchTreeRoot();

        trees.Handoff.Hold("flight-1", ATreeAt(trees.Root.For("flight-1"), "first"));
        var second = trees.Handoff.Hold("flight-1", ATreeAt(trees.Root.For("flight-1"), "second"));

        await Assert.That(second).IsNotNull();
        await Assert.That(File.ReadAllText(Path.Combine(second!.Path, "src", "greet.py")))
            .IsEqualTo("second");
    }

    [Test]
    public async Task The_handoff_root_is_not_inside_the_root_that_gets_swept()
    {
        // The whole arrangement in one assertion. A handoff root under the
        // working root is a handoff root the sweep deletes.
        await Assert.That(HandoffRoot.DefaultPath())
            .DoesNotStartWith(WorkingTreeRoot.DefaultPath());
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan by) => _now += by;
    }
}
