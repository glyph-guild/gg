using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// Ready answers, and "not ready" is an answer rather than a failure.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner's decision, slice fifty-eight:</b> <i>"the hook always answers
/// ready/not-ready; it may additionally report named values, one of which is an
/// address."</i> So the point is a REPORT and not a return code — which is also
/// why it is called <c>ready</c> rather than <c>verify</c>: <c>verify</c> is
/// already a pool action, <i>"inspect a pool member and attest what was found"</i>,
/// about the member rather than the customer's app.
/// </para>
/// <para>
/// <b>THREE OUTCOMES, NOT TWO, and this is the one that would have been got
/// wrong.</b> <c>ready=no</c> is a stack that is not up yet. A hook that could
/// not answer at all is a different fact, and collapsing them would make a
/// broken hook indistinguishable from a slow stack for ever. Article XI one
/// layer out: silently absent must never read as a measured answer.
/// </para>
/// <para>
/// <b>Silence is not readiness.</b> A hook that exits zero and says nothing has
/// not answered. Reading that as ready is how a flight is handed an environment
/// nobody checked — and it is the easiest mistake to make, because exit zero
/// usually means yes.
/// </para>
/// </remarks>
public class ReadyAnswersRatherThanExitsTests
{
    private static StackScript.Performance Exited(int code) =>
        new(StackOutcomes.Exited, code, TimeSpan.FromSeconds(1), Survived: false);

    [Test]
    public async Task Ready_yes_is_read_as_ready()
    {
        var report = StackScript.ReadReady(Exited(0), "ready=yes\n");

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Yes);
    }

    [Test]
    public async Task Ready_no_is_read_as_not_ready_and_is_not_an_error()
    {
        var report = StackScript.ReadReady(Exited(0), "ready=no\n");

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.No)
            .Because("a stack that is not up yet is something the hook SAID, and the whole "
                   + "point of a report is that saying no is allowed.");

        await Assert.That(report.Readiness).IsNotEqualTo(StackScript.Readiness.Unanswered)
            .Because("'no' is a measured answer: the hook looked and the stack was not "
                   + "answering. Conflating it with silence loses the measurement.");
    }

    [Test]
    public async Task A_non_zero_exit_is_unanswered_however_it_printed()
    {
        // NEVER READ AS `no`. A hook that failed is not a stack that is down,
        // and a flight told "not ready" when the truth is "nothing asked" waits
        // for something that will never happen.
        foreach (var said in (string[]) ["", "ready=no\n", "ready=yes\n", "nonsense\n"])
        {
            var report = StackScript.ReadReady(Exited(3), said);

            await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Unanswered)
                .Because("the hook itself failed, so whatever it managed to print is not a "
                       + "measurement of the stack.");
        }
    }

    [Test]
    public async Task A_timeout_is_unanswered()
    {
        var report = StackScript.ReadReady(
            new StackScript.Performance(
                StackOutcomes.Timeout, null, TimeSpan.FromMinutes(10), Survived: false),
            "ready=yes\n");

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Unanswered)
            .Because("a hook still running when patience ran out has not finished looking, "
                   + "and a `ready=yes` it printed on the way is not a conclusion.");
    }

    [Test]
    public async Task Exiting_zero_and_saying_nothing_is_unanswered()
    {
        // THE EASIEST MISTAKE TO MAKE, because exit zero usually means yes.
        // Here it means the hook ran and did not implement the contract, and
        // reading it as ready hands a flight an environment nobody checked.
        foreach (var silent in (string[]) ["", "\n", "Creating network...\ndone\n"])
        {
            var report = StackScript.ReadReady(Exited(0), silent);

            await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Unanswered)
                .Because("silence is not readiness - the hook said nothing about the stack, "
                       + "which is a different fact from saying it is up.");
        }
    }

    [Test]
    public async Task The_last_answer_wins()
    {
        // A HOOK THAT POLLS PRINTS AS IT GOES. `attach` is told to poll until
        // the stack answers, and a `ready` written the same way may print its
        // progress - so what is true is what it said last, exactly as the
        // runner reads the LAST document.proposal of a flight.
        var report = StackScript.ReadReady(Exited(0), "ready=no\nready=no\nready=yes\n");

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Yes);
    }
}
