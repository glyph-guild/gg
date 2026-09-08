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
    private static RunnerRow Row(string label) => new(
        Mine: false, Yours: false, Machine: false, RegisteredBy: "somebody",
        Id: "01a06385-322f-7371-93a2-ce35db5c4fbe", Label: label, ParkedBecause: "",
        Here: " ", Runner: "01a06385  " + label, State: "offline", Work: "",
        Labels: "", Heard: "");

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

            await Assert.That(said.Contains("not something the control plane reports",
                                  StringComparison.Ordinal)
                           || said.Contains("Suggested rather than reported",
                                  StringComparison.Ordinal)
                           || said.Contains("is not.", StringComparison.Ordinal))
                .IsTrue()
                .Because($"'{label}' offered a command with nothing saying gg is guessing.");
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
        await Assert.That(absence).Contains("no log here to read")
            .Because("the sentence that was already there says WHY there is nothing, and the "
                   + "command says what to do about it. Both, or the second reads as an "
                   + "error message.");
    }
}
