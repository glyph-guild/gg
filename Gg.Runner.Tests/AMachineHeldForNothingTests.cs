using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A machine is not held out of service for a preview the loop just said is not
/// there.
/// </summary>
/// <remarks>
/// <para>
/// <b>GG-531, and the new check is what made it visible.</b> The loop failed
/// with <i>"nothing answers at http://localhost:8080: Connection refused"</i> —
/// and four seconds later the same runner logged <i>"holding for GG-531's
/// preview at https://jdapp-03.goodgrief.dev … takes no work until that flight's
/// gate is answered"</i>. Two adjacent lines, the second asking a person to
/// review the address the first says nothing answers at, and a four-core host
/// out of service for twelve hours to protect it.
/// </para>
/// <para>
/// <b>It was always wrong and only now says so.</b> Before <c>PreviewAnswers</c>
/// the loop claimed <c>completed</c>, so the hold was at least consistent with
/// what the platform believed. Measuring made the contradiction legible rather
/// than creating it — GG-522 and GG-524 held their machines for a 502 too, and
/// nothing in the log admitted it.
/// </para>
/// <para>
/// <b>Measured, not inferred from the outcome.</b> A loop can fail for reasons
/// that leave a perfectly good preview standing — an obligation refused, a
/// destination that would not take it — and that preview is still somebody's to
/// look at. What forfeits the hold is the address not answering, which is the
/// one thing already measured by then.
/// </para>
/// <para>
/// <b>Null is "nobody asked", and keeps the old behaviour.</b> Every caller that
/// does not probe gets what it had, so this adds a reason to RELEASE a machine
/// and takes none away.
/// </para>
/// </remarks>
public class AMachineHeldForNothingTests
{
    private static readonly IReadOnlyList<string> Previews = [FactKinds.PreviewUrl];

    private static ExposureServed Serving() => new()
    {
        Url = "https://jdapp-03.example.dev",
        Origin = "http://localhost:8080",
        Exposure = "jdapp",
        Slot = "03",
    };

    [Test]
    public async Task A_preview_that_does_not_answer_does_not_hold_the_machine()
    {
        await Assert.That(TreeRetention.HoldsItsMachine(Serving(), Previews, answered: false))
            .IsFalse()
            .Because("GG-531 said 'nothing answers at http://localhost:8080' and then held a "
                   + "four-core host for twelve hours so somebody could go and look at it.");
    }

    [Test]
    public async Task A_preview_that_answers_still_holds_it()
    {
        // THE POISON TWIN, and the whole reason the hold exists: what that
        // address is serving is somebody's unreviewed work, on this tree and
        // this port, so going back for work would pull both out from under them.
        await Assert.That(TreeRetention.HoldsItsMachine(Serving(), Previews, answered: true))
            .IsTrue();
    }

    [Test]
    public async Task A_preview_nobody_probed_holds_it_exactly_as_before()
    {
        // NULL IS "NOBODY ASKED". Callers that do not measure must behave as they
        // did, or this becomes a change to when machines are held generally
        // rather than a narrow refusal to hold one for nothing.
        await Assert.That(TreeRetention.HoldsItsMachine(Serving(), Previews, answered: null))
            .IsEqualTo(TreeRetention.HoldsItsMachine(Serving(), Previews));
        await Assert.That(TreeRetention.HoldsItsMachine(Serving(), Previews, answered: null))
            .IsTrue();
    }

    [Test]
    public async Task It_takes_no_hold_away_from_a_kind_that_never_had_one()
    {
        // A kind that asked for no preview, and a slot that never served an
        // address, were both already false - and must stay false for the same
        // reasons rather than newly false for this one.
        foreach (var answered in (bool?[])[null, true, false])
        {
            await Assert.That(TreeRetention.HoldsItsMachine(Serving(), produces: null, answered))
                .IsFalse();
            await Assert.That(TreeRetention.HoldsItsMachine(serving: null, Previews, answered))
                .IsFalse();
        }
    }
}
