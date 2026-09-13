using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The modal names where a remote runner's output is, and calls it a guess.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0013 Decision 2, and it needs no wire change at all.</b> gg is what
/// puts a runner's log where it is, so the console can compose the command from
/// the label and its own conventions. Nothing new crosses.
/// </para>
/// <para>
/// <b>The suffix decides the convention, and the walk is why.</b> On the
/// development fleet the pool host's runner is a systemd unit: its output is in
/// the journal, and <c>~/.local/state/good-grief/runner.log</c> does not exist
/// there at all. Offering the file path for every runner would have been a
/// command that fails on every properly supervised one.
/// </para>
/// </remarks>
public class SuggestedLogCommandTests
{
    private static RunnerRow Row(string label, string work = "") => new(
        Mine: false, Yours: false, Machine: false, RegisteredBy: "somebody",
        Id: "01a06385-322f-7371-93a2-ce35db5c4fbe", Label: label, ParkedBecause: "",
        Here: " ", Runner: "01a06385  " + label, State: "offline", Work: work,
        Labels: "", Heard: "");

    /// <summary>A fleet with one runner nobody here started.</summary>
    private static AppState Flying(string label, string? flying) => new()
    {
        RunnerSelected = 0,
        Runners = new Gg.Contracts.RunnerList
        {
            Runners =
            [
                new Gg.Contracts.RunnerSummary
                {
                    RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                    Label = label,
                    State = flying is null
                        ? Gg.Contracts.RunnerStates.Idle
                        : Gg.Contracts.RunnerStates.Busy,
                    CurrentFlightNumber = flying,
                },
            ],
        },
    };

    [Test]
    public async Task A_pool_runner_is_pointed_at_the_unit_gg_ships()
    {
        var said = RunnerDetails.Suggestion(Row("vmlinux001:maintain"));

        await Assert.That(said).Contains("journalctl -u gg-runner-maintain")
            .Because("that unit is in deploy/pool-host, so naming it is a fact about our own "
                   + "packaging rather than a guess about their host.");
        await Assert.That(said).Contains("ssh vmlinux001")
            .Because("the machine is the label with the suffix taken off.");
        await Assert.That(said).DoesNotContain("runner.log")
            .Because("the file is not there. A supervised runner's output is the journal's, "
                   + "and this is exactly what the walk found.");
    }

    [Test]
    public async Task A_hand_flown_runner_is_pointed_at_the_file()
    {
        var said = RunnerDetails.Suggestion(Row("laptop-7:hand"));

        await Assert.That(said).Contains("~/.local/state/good-grief/runner.log")
            .Because("AttendedRunner is a person flying by hand, so a console started it and "
                   + "gg wrote the file.");
        await Assert.That(said).Contains("ssh laptop-7");
        await Assert.That(said).DoesNotContain("journalctl")
            .Because("offering both here would be hedging about something the name settles.");
    }

    [Test]
    public async Task A_bare_machine_name_reports_the_ambiguity_rather_than_picking()
    {
        // `gg runner up` can be started by either, and the console cannot tell.
        // Choosing one would be right about half a fleet.
        var said = RunnerDetails.Suggestion(Row("vmlinux001"));

        await Assert.That(said).Contains("runner.log");
        await Assert.That(said).Contains("journalctl");
        await Assert.That(said).Contains("One of the two")
            .Because("two commands with no sentence saying why is a person guessing which "
                   + "one their fleet uses.");
    }

    [Test]
    public async Task It_is_offered_as_a_suggestion_rather_than_as_a_fact()
    {
        // S34.2-02. XDG_STATE_HOME may be set, the host may be a container, and
        // the label may not resolve to anything ssh can reach. A line that reads
        // like a fact here is the same defect as a state that collapses two
        // silences.
        foreach (var label in (string[])["vmlinux001", "vmlinux001:maintain", "laptop-7:hand"])
        {
            var said = RunnerDetails.Suggestion(Row(label));

            await Assert.That(said.Contains("Suggested", StringComparison.OrdinalIgnoreCase)
                || said.Contains("not something the control plane", StringComparison.Ordinal))
                .IsTrue()
                .Because("gg is guessing about somebody else's machine and has to say so. The "
                       + "wording moved when the paragraph was cut; the admission did not. "
                       + "Said: " + said);
        }
    }

    [Test]
    public async Task No_credential_of_ours_appears_in_any_of_them()
    {
        // The person's own access to their own host is the channel. gg holding
        // one here would be the thing ReaderSessions' shape exists to avoid.
        foreach (var label in (string[])["vmlinux001", "vmlinux001:maintain", "laptop-7:hand"])
        {
            var said = RunnerDetails.Suggestion(Row(label));

            foreach (var smell in (string[])["-i ", ".pem", "token", "password", "Bearer"])
            {
                await Assert.That(said).DoesNotContain(smell)
                    .Because($"'{label}' composed a command carrying '{smell}'.");
            }
        }
    }

    [Test]
    public async Task A_row_the_control_plane_never_reported_gets_no_suggestion()
    {
        // The invented row for a runner registered on this machine that the
        // fleet has never heard of. There is no machine to reach, and a
        // suggestion composed from a guess is what this exists not to offer.
        await Assert.That(RunnerDetails.Suggestion(Row(""))).IsEmpty();
    }

    [Test]
    public async Task The_absence_text_carries_it_for_a_runner_that_is_not_ours()
    {
        // The suggestion has to reach the pane, or it is a function nobody calls.
        var fleet = new[]
        {
            new RunnerSummary
            {
                RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                Label = "vmlinux001:maintain",
                State = RunnerStates.Offline,
            },
        };

        var state = new AppState
        {
            Runners = new RunnerList { Runners = fleet },
            RunnerSelected = 0,
        };
        var absence = RunnerDetails.LogAbsence(state);

        await Assert.That(absence).Contains("journalctl -u gg-runner-maintain")
            .Because("a suggestion the pane never renders is a function nobody calls.");
        await Assert.That(absence).Contains("No log is available")
            .Because("the sentence says WHY there is nothing and the command says what to do "
                   + "about it - both, or the second reads as an error message. It used to "
                   + "open with a paragraph; the property is that it says why, not how long "
                   + "it takes to.");
    }

    [Test]
    public async Task A_runner_that_is_flying_something_is_offered_a_way_to_watch_it()
    {
        // THE GAP THIS CLOSES. Everything the slice built was reachable from
        // nowhere: a person arriving at this modal found an ssh command and no
        // way to use the channel, the seal or the runner's session.
        //
        // AND IT IS THE KEY NOW, NOT THE COMMAND. When this was written the
        // console could only NAME `gg runner watch`; pressing `w` does it, from
        // this modal, without leaving. The property is unmoved - a capability
        // nobody can see is one nobody learns - and the letter is read off the
        // keymap rather than typed here, so the two cannot drift.
        var said = RunnerDetails.LogAbsence(Flying("vmlinux001", "GG-71"));

        await Assert.That(said).Contains("`w`")
            .Because("this modal is where somebody arrives wanting to know what a machine "
                   + "they cannot reach is doing. Said: " + said);

        await Assert.That(said).Contains("GG-71")
            .Because("naming what it is flying is what makes the offer about something. "
                   + "Said: " + said);
    }

    [Test]
    public async Task A_runner_flying_nothing_is_offered_the_key_too()
    {
        // THE THIRD CORRECTION OF ONE SENTENCE, and each one was right about
        // the code it was written against. First the verb showed only while
        // something was flying. Then always, so a person on an idle fleet -
        // which is most of the time - would learn the capability exists. Then
        // never, because it had become a KEY and the key was not bound here, and
        // a pane offering one that is not live teaches somebody to distrust the
        // pane.
        //
        // NOW THE KEY IS BOUND HERE. A runner answers while it is beating, so
        // attaching to a machine that is waiting is not only possible, it is the
        // reason the whole thing exists: it is how somebody sees work arrive
        // rather than finding out afterwards that it did.
        var said = RunnerDetails.LogAbsence(Flying("vmlinux001", flying: null));

        await Assert.That(said).StartsWith("No log is available when idle.")
            .Because("there is still no log to fetch, and the first sentence is the true "
                   + "one about what is HERE.");

        await Assert.That(said).Contains("`w`")
            .Because("and what to do about it is a key that is live right now, read off the "
                   + "keymap so the two cannot drift. Said: " + said);
    }
}
