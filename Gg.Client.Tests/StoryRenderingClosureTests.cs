using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Every kind the control plane can emit renders in this build.
/// </summary>
/// <remarks>
/// <para>
/// <b>Over the vocabulary, not over the kinds the tests happened to produce.</b>
/// A rendering suite exercises the kinds somebody thought of; the control plane
/// emits whatever <c>StoryKinds</c> declares, and the gap between those two sets
/// is where a person meets a blank line in the middle of a history.
/// </para>
/// <para>
/// <b>This is the guard that lets the contract throw.</b>
/// <c>FlightStory.Sentence</c> refuses a kind it does not know — <i>"a renderer
/// that shrugs at a kind it does not know turns a governed record into silence,
/// and silence reads as health"</i> — which is right, and makes an unrendered
/// kind an exception in front of a person rather than a missing line. So the
/// closure has to be asserted here, where a build can fail instead.
/// </para>
/// <para>
/// <b>Both directions.</b> A kind with no sentence is a story that throws; a
/// stage-less kind the stage table has not been told about is the same failure
/// one function over, and <c>FlightStages.Of</c> throws for exactly that reason.
/// </para>
/// </remarks>
public class StoryRenderingClosureTests
{
    // ---- S32.4-04 ----

    [Test]
    public async Task Every_declared_kind_has_a_sentence()
    {
        var silent = new List<string>();

        foreach (var kind in StoryKinds.All)
        {
            try
            {
                // NO PARAMS, which is the harder case. A kind whose grammar reads
                // its parameters must still render from a record written by an
                // older writer - missing params are an older record, not a broken
                // one - so this is the degradation asserted at the same time.
                var sentence = FlightStory.Sentence(kind, []);

                if (sentence is not { Length: > 0 })
                {
                    silent.Add(kind);
                }
            }
            catch (InvalidOperationException)
            {
                silent.Add(kind);
            }
        }

        await Assert.That(silent).IsEmpty()
            .Because("a kind the control plane can emit and this build cannot word is an "
                   + "exception in front of a person, because Sentence refuses rather than "
                   + "shrugs. Found: " + string.Join(", ", silent));
    }

    [Test]
    public async Task Every_declared_kind_belongs_to_a_stage_or_says_it_belongs_to_none()
    {
        // NULL IS AN ANSWER AND A THROW IS NOT. `taken-over`, `hold-expired` and
        // `pool-incident` interrupt at any stage and belong to none of the six;
        // a kind nobody put in the table throws, which is a story that cannot be
        // composed at all rather than one with a gap.
        var unplaced = new List<string>();

        foreach (var kind in StoryKinds.All)
        {
            try
            {
                _ = FlightStages.Of(kind);
            }
            catch (InvalidOperationException)
            {
                unplaced.Add(kind);
            }
        }

        await Assert.That(unplaced).IsEmpty()
            .Because("composing the story is what throws here, so this is not a missing line - "
                   + "it is the whole flight becoming unreadable. Found: "
                   + string.Join(", ", unplaced));
    }

    [Test]
    public async Task A_kind_nobody_declared_is_refused_rather_than_rendered_blank()
    {
        // THE GUARD MADE TO FAIL, in the only direction it can be. The two above
        // pass over a closed vocabulary and would pass over an empty one; this
        // asserts the refusal they rely on actually happens.
        await Assert.That(() => FlightStory.Sentence("runner-sneezed", []))
            .Throws<InvalidOperationException>();

        await Assert.That(() => FlightStages.Of("runner-sneezed"))
            .Throws<InvalidOperationException>();
    }
}
