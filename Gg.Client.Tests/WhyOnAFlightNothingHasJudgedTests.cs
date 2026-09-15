using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// An attribution with no obligations says nothing has been judged, not that
/// nothing governs.
/// </summary>
/// <remarks>
/// <para>
/// <b>This branch could not be reached until the control plane learned to send
/// it.</b> <c>AttributionReader.ReadAsync</c> answers null rather than an empty
/// list, and the endpoint turned that null into a 404 — so
/// <c>gg why GG-117</c> said <i>"No flight GG-117"</i> about a flight a runner
/// was holding. good-grief#418 fixed that: a resolved flight with nothing
/// judged now answers with its number, its pinned envelope and an empty
/// obligation list.
/// </para>
/// <para>
/// <b>Which makes the sentence that was already here wrong.</b> It read <i>"This
/// envelope declares no obligation, so nothing governed this flight"</i> — an
/// assertion about the ENVELOPE, from a rendering that cannot know. An empty
/// list has two causes now: an envelope that declares none, and a flight
/// nothing has evaluated yet. The second is the common one, because every
/// flight is in that state between being opened and being judged.
/// </para>
/// <para>
/// <b>So the sentence has to be true of both.</b> A client that guessed which
/// would be re-deriving a fact it was not sent — the same rule this verb
/// already keeps about verdicts: <i>"a client that re-evaluated a predicate in
/// order to explain it could explain a verdict it did not produce"</i>. It
/// reports the absence and names the two readings rather than picking one.
/// </para>
/// </remarks>
public class WhyOnAFlightNothingHasJudgedTests
{
    private static FlightAttribution NothingJudged() => new()
    {
        FlightNumber = "GG-117",
        EnvelopeVersion = "v7",
        Obligations = [],
    };

    [Test]
    public async Task It_does_not_claim_the_envelope_declares_nothing()
    {
        // THE FALSE HALF. For a running flight this is a confident statement
        // about a document the rendering never saw - and it is worse than the
        // 404 it replaced, because it sounds authoritative.
        var text = VerbOutput.ToText(new VerbResult.Why(NothingJudged()));

        await Assert.That(text).DoesNotContain("declares no obligation")
            .Because("the attribution carries no obligations; that is not the same fact as "
                   + "an envelope that declares none, and this rendering cannot tell them "
                   + "apart.");
    }

    [Test]
    public async Task It_says_nothing_has_been_judged()
    {
        var text = VerbOutput.ToText(new VerbResult.Why(NothingJudged()));

        await Assert.That(text).Contains("judged")
            .Because("the absence is the answer, and a person who ran this verb on a flight "
                   + "that looks stuck needs to be told which absence it is.");
    }

    [Test]
    public async Task And_still_says_which_flight_and_which_envelope()
    {
        // WHAT THE EMPTY LIST DOES NOT TAKE AWAY. The two facts the control
        // plane could answer are still answered, so the verb is not reduced to
        // a shrug.
        var text = VerbOutput.ToText(new VerbResult.Why(NothingJudged()));

        await Assert.That(text).Contains("GG-117");
        await Assert.That(text).Contains("v7");
    }

    [Test]
    public async Task A_halt_still_leads()
    {
        // THE ORDER THAT MUST NOT MOVE. A halted flight with no attributions
        // has one thing worth reading first, and it is not the absence.
        var text = VerbOutput.ToText(new VerbResult.Why(
            NothingJudged() with { Halt = "the runner could not reach the tracker" }));

        await Assert.That(text).Contains("HALTED");
        await Assert.That(text.IndexOf("HALTED", StringComparison.Ordinal))
            .IsLessThan(text.IndexOf("judged", StringComparison.Ordinal))
            .Because("why it stopped comes before what was never measured.");
    }
}
