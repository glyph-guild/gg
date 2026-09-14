using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// When a runner says nothing about a credential, the sentence names every
/// reason it could have been silent.
/// </summary>
/// <remarks>
/// <para>
/// <b>It named two and there were three.</b> The sentence offered "running a gg
/// that predates this" and "the ask did not reach it", and the third — a runner
/// new enough to have the arm whose own file does not say
/// <c>accept-configured</c> — was the one a person is most likely to hit,
/// because opting in is a decision somebody has to have made on that machine
/// and absence is the default.
/// </para>
/// <para>
/// <b>It cost a real diagnosis.</b> A send to a live, beating, idle runner
/// reported silence, and the sentence sent the reader to compare versions on a
/// machine whose version may have been fine. The runner answers that case now,
/// so the accept-configured sentence is reachable — but a runner in the field
/// today still cannot, and this text is what those people get until they
/// update.
/// </para>
/// <para>
/// <b>Asserted on the words rather than on an outcome code</b>, because the
/// words are the whole deliverable here: <c>NotWritten</c> was already correct
/// and told nobody anything.
/// </para>
/// </remarks>
public class SilenceFromARunnerNamesItsCausesTests
{
    [Test]
    public async Task Silence_names_the_setting_that_is_the_likeliest_cause()
    {
        var said = SendACredential.SaidWhenNothingCameBack("vmlinux001");

        await Assert.That(said).Contains("accept-configured", StringComparison.Ordinal)
            .Because("a runner that has the arm and no keeper is silent by design, and that "
                   + "is fixed in a file on that machine rather than by an upgrade.");
    }

    [Test]
    public async Task Silence_still_names_a_runner_too_old_to_have_the_arm()
    {
        // THE CAUSE THAT MUST NOT BE DROPPED WHILE ADDING ONE. A runner one
        // version behind has no arm and cannot answer at all, so it is silent
        // whatever its file says - and every runner in the field was that
        // runner until it was updated.
        var said = SendACredential.SaidWhenNothingCameBack("vmlinux001");

        await Assert.That(said).Contains("predates", StringComparison.Ordinal);
    }

    [Test]
    public async Task Silence_says_nothing_was_written_and_nothing_left_behind()
    {
        // THE HALF A PERSON ACTS ON FIRST. Somebody who has just handed a token
        // to a machine that said nothing wants to know whether it is now
        // somewhere they did not intend, before they want to know why.
        var said = SendACredential.SaidWhenNothingCameBack("vmlinux001");

        await Assert.That(said).Contains("Nothing was written", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_runner_is_named_so_a_fleet_of_them_is_actionable()
    {
        var said = SendACredential.SaidWhenNothingCameBack("vmlinux001");

        await Assert.That(said).Contains("vmlinux001", StringComparison.Ordinal);
    }
}
