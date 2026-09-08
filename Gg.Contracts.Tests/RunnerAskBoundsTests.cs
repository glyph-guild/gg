using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What comes back is bounded by the contract, not by whatever carried it.
/// </summary>
/// <remarks>
/// <b>Here rather than in a transport, because a second transport must not get
/// to disagree.</b> A limit that lived in the WebRTC path would be re-invented,
/// differently, by the dead-drop — and the first person to notice would be
/// somebody whose tail was silently a different length depending on how it
/// arrived.
/// </remarks>
public class RunnerAskBoundsTests
{
    /// <summary>
    /// A bound, read rather than named.
    /// </summary>
    /// <remarks>
    /// <b>Through reflection because they are <c>const</c>.</b> A comparison
    /// against a constant folds at compile time, which TUnit refuses outright and
    /// is right to: an assertion the compiler can answer is not an assertion. The
    /// reflection is what makes these tests about the values the contract
    /// actually ships.
    /// </remarks>
    private static int Bound(string name) =>
        (int)typeof(RunnerAskBounds).GetField(name, BindingFlags.Public | BindingFlags.Static)!
            .GetRawConstantValue()!;

    [Test]
    public async Task The_bounds_are_declared_and_are_small_on_purpose()
    {
        var lines = Bound(nameof(RunnerAskBounds.MaxLines));
        var bytes = Bound(nameof(RunnerAskBounds.MaxBytes));

        await Assert.That(lines).IsGreaterThan(0);
        await Assert.That(bytes).IsGreaterThan(0);

        // A TAIL, NOT A LOG SHIPPER. The numbers being small is the design:
        // anything needing more wants Decision 2's command and the person's own
        // credentials, not this channel.
        await Assert.That(lines).IsLessThanOrEqualTo(1000)
            .Because("a bound large enough to move a whole log is not a bound.");
        await Assert.That(bytes).IsLessThanOrEqualTo(1024 * 1024)
            .Because("the same argument in bytes, and this one is what a hostile runner "
                   + "would push against.");
    }

    [Test]
    public async Task A_tail_says_when_a_bound_cut_it_short()
    {
        // TRUNCATED IS A FACT, NOT AN APOLOGY. A tail that stopped at the bound
        // and one that was genuinely that short read identically otherwise, and
        // collapsing two silences is this system's named worst failure.
        var cut = new LogTail { Lines = ["a", "b"], Truncated = true };
        var whole = new LogTail { Lines = ["a", "b"], Truncated = false };

        await Assert.That(cut.Truncated).IsNotEqualTo(whole.Truncated)
            .Because("without this member the two are the same value.");
    }

    [Test]
    public async Task The_ask_carries_the_line_count_the_bound_applies_to()
    {
        // The bound is about a number the CALLER sends, so the ask has to have
        // one for anything to bound.
        var ask = new TailLogAsk { Lines = Bound(nameof(RunnerAskBounds.MaxLines)) };

        await Assert.That(ask.Lines).IsEqualTo(Bound(nameof(RunnerAskBounds.MaxLines)));
    }
}
