namespace Gg.Client.Tests;

/// <summary>
/// A machine with no tracker is told how to get one, the way a machine with no
/// forge already is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported by a person whose Browse tab was dead.</b> The pane said <i>"No
/// tracker is configured to browse on this machine"</i> and <c>gg doctor</c> —
/// the verb they ran to find out why — answered:
/// </para>
/// <code>
/// ok    trackers   no tracker is declared, so a flight about a work item reaches
///                  an agent with nothing to read it with - ordinary on a machine
///                  whose flights name none
/// </code>
/// <para>
/// <b>Marked ok, with no fix.</b> Two surfaces, one fact, opposite verdicts, and
/// the one a person runs to diagnose offered nothing to do about it.
/// </para>
/// <para>
/// <b>The sentence is right and is kept.</b> On a machine that never flies a
/// work item this really is ordinary, which is why the answer is not to start
/// blocking. What was missing is the other half: how to declare one, for the
/// machine where it is not ordinary at all.
/// </para>
/// <para>
/// <b>The precedent is four lines up its own output.</b> <c>forge</c> with
/// nothing configured is a <c>warn</c> carrying <c>gg config set vcs-hosts</c>;
/// <c>trackers</c> with nothing configured was an <c>ok</c> carrying nothing.
/// The two are the same situation and disagreed about it. And the comment that
/// defends this check's leniency defends <b>Blocking</b>, which a warn leaves
/// exactly as it was: a doctor that exited non-zero on a laptop is still a verb
/// nobody runs.
/// </para>
/// </remarks>
public class ADeclaredNothingIsStillAnAnswerTests
{
    // NOTHING UNRESOLVED, so the "declared but not held here" arm stays out of
    // the way: what is under test is the arm where nothing is declared at all.
    private static DoctorCheck Trackers(MachineRole role) =>
        Doctor.TrackerCheck(role, _ => false);

    [Test]
    public async Task No_tracker_declared_warns_rather_than_passing_in_silence()
    {
        var check = Trackers(MachineRole.None);

        await Assert.That(check.Passed).IsFalse()
            .Because("the browse pane refuses over this exact fact, so a doctor calling it ok "
                   + "leaves a person with two answers and no way to tell which is wrong.");
    }

    [Test]
    public async Task And_says_how_to_declare_one()
    {
        var check = Trackers(MachineRole.None);

        await Assert.That(check.Fixable).IsTrue();
        await Assert.That(check.Fix).Contains("intent-hosts")
            .Because("the setting is the answer, and a person who has just been told they "
                   + "have no tracker cannot be expected to know its name.");
    }

    [Test]
    public async Task But_it_never_blocks()
    {
        // THE HALF THE OLD COMMENT WAS DEFENDING, and it is untouched. A
        // machine may simply not be one that reads work items; a doctor that
        // exited non-zero on a laptop is a verb nobody runs.
        var check = Trackers(MachineRole.None);

        await Assert.That(check.Blocking).IsFalse();
    }

    [Test]
    public async Task And_still_says_when_having_none_is_ordinary()
    {
        // THE SENTENCE IS KEPT. What was wrong was the absence of a remedy
        // beside it, not the explanation - a warn that only said "no tracker"
        // would make every runner host look broken.
        await Assert.That(Trackers(MachineRole.None).Detail).Contains("ordinary");
    }

    [Test]
    public async Task A_machine_that_has_one_is_untouched()
    {
        var reading = Trackers(MachineRole.None with
        {
            Trackers = [new DeclaredTracker
            {
                Key = "ado",
                Host = "https://dev.azure.com/acme/Widgets",
                Locator = "local:acme/widgets",
            }],
        });

        await Assert.That(reading.Passed).IsTrue();
        await Assert.That(reading.Fixable).IsFalse();
    }
}
