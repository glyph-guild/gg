using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What comes back from a runner is hostile text, and it is stripped at ingress.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0006's third hazard applied to a much larger surface.</b> gg already
/// strips control sequences from a runner's display NAME - forty characters. A
/// tail is thousands of lines, it goes straight into a terminal, and it can
/// contain text addressed to a model.
/// </para>
/// <para>
/// <b>The rule lives on the contract for <see cref="ControlText"/>'s own
/// reason</b>: the control plane strips at ingress and gg strips what it renders,
/// and those must be one rule rather than two that agree today.
/// </para>
/// <para>
/// <b>No raw escape character appears in this file</b>, per the same discipline
/// the console holds: a file that composes a sequence out of a literal control
/// character is one nobody can review by reading, and one that survives a copy
/// and paste into somewhere it should not.
/// </para>
/// </remarks>
public class RunnerSaidIsStrippedTests
{
    private const string Escape = "\u001b";

    [Test]
    public async Task An_escape_sequence_in_a_tail_does_not_survive()
    {
        var said = new RunnerSaid
        {
            Kind = RunnerAskKinds.TailLog,
            Tail = new LogTail
            {
                Lines = [$"{Escape}[2Jcleared your screen", "ordinary line"],
                Truncated = false,
            },
        };

        var clean = said.Stripped();

        await Assert.That(clean.Tail!.Lines[0]).DoesNotContain(Escape)
            .Because("this is rendered in a terminal, and a runner is hostile.");
        await Assert.That(clean.Tail.Lines[1]).IsEqualTo("ordinary line")
            .Because("stripping that also destroyed the ordinary line would make the "
                   + "feature useless in the name of safety.");
    }

    [Test]
    public async Task A_diagnosis_is_stripped_too()
    {
        // TIER 2C IS THE UNBOUNDED ONE. ADR-0013 classifies BoundBroken,
        // WorkspaceFailed and ControlPlaneRefused as free-form strings that can
        // contain anything - which is the reason they cannot be facts.
        var said = new RunnerSaid
        {
            Kind = RunnerAskKinds.Status,
            Status = new RunnerStatusReport
            {
                Doing = $"waiting{Escape}[31m",
                Diagnosis = $"{Escape}]0;retitled your windowcould not reach the forge",
                At = DateTimeOffset.UnixEpoch,
            },
        };

        var clean = said.Stripped();

        await Assert.That(clean.Status!.Doing).DoesNotContain(Escape);
        await Assert.That(clean.Status.Diagnosis).DoesNotContain(Escape);
        await Assert.That(clean.Status.Diagnosis).Contains("could not reach the forge")
            .Because("the diagnosis is the whole point of asking; stripping must remove "
                   + "sequences and not meaning.");
    }

    [Test]
    public async Task Line_breaks_survive_a_diagnosis_and_not_a_line()
    {
        // A TAIL IS ALREADY LINES. A newline inside one would let a runner forge
        // extra rows in whatever renders them; a diagnosis is one string that may
        // legitimately have them.
        var said = new RunnerSaid
        {
            Kind = RunnerAskKinds.Status,
            Status = new RunnerStatusReport
            {
                Doing = "waiting",
                Diagnosis = "first\nsecond",
                At = DateTimeOffset.UnixEpoch,
            },
        };

        await Assert.That(said.Stripped().Status!.Diagnosis).Contains("\n");

        var tail = new RunnerSaid
        {
            Kind = RunnerAskKinds.TailLog,
            Tail = new LogTail { Lines = ["one\ntwo"], Truncated = false },
        };

        await Assert.That(tail.Stripped().Tail!.Lines[0]).DoesNotContain("\n")
            .Because("a line that can contain a newline is a line that can forge a second "
                   + "one in whatever renders it.");
    }

    [Test]
    public async Task Stripping_keeps_what_is_not_text()
    {
        // The liveness half: a strip that returned an empty answer would satisfy
        // every assertion above.
        var at = DateTimeOffset.UnixEpoch.AddDays(1);
        var said = new RunnerSaid
        {
            Kind = RunnerAskKinds.TailLog,
            Tail = new LogTail { Lines = ["a", "b"], Truncated = true },
        };

        var clean = said.Stripped();

        await Assert.That(clean.Kind).IsEqualTo(RunnerAskKinds.TailLog);
        await Assert.That(clean.Tail!.Lines.Count).IsEqualTo(2);
        await Assert.That(clean.Tail.Truncated).IsTrue()
            .Because("truncation is a fact about the answer, not part of its text.");

        var status = new RunnerSaid
        {
            Kind = RunnerAskKinds.Status,
            Status = new RunnerStatusReport { Doing = "idle", Diagnosis = null, At = at },
        }.Stripped();

        await Assert.That(status.Status!.At).IsEqualTo(at);
        await Assert.That(status.Status.Diagnosis).IsNull()
            .Because("no diagnosis and an empty one are different facts.");
    }
}
