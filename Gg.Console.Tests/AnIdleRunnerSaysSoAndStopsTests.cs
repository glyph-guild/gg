using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The runner log pane says the one thing that is true, and stops.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was a wall.</b> For a runner this console did not start — which is
/// every fleet runner — the log pane rendered a sentence about why there is no
/// log, a `gg runner watch` line with a paragraph under it, and two `ssh`
/// commands with a paragraph under those. Six lines of prose and three commands,
/// in a box where a log goes, for a machine that is sitting idle.
/// </para>
/// <para>
/// <b>And a third of it had gone stale.</b> The watch paragraph said a runner
/// answers "only for a flight opened to be watched", which stopped being true
/// when watching stopped requiring an attended flight. Prose in a pane ages
/// exactly like prose in a comment and nothing was comparing it to anything.
/// </para>
/// <para>
/// <b>What survives is what a person can act on.</b> An idle runner has no log
/// to fetch and nothing to watch, so the honest answer is one line. The `ssh`
/// suggestion stays because S34.2-01 is proven on it — the modal names where
/// the output is, and a person's own access is the channel — but it is offered
/// once rather than twice and without the essay.
/// </para>
/// </remarks>
public class AnIdleRunnerSaysSoAndStopsTests
{
    private static AppState Fleet(string label, string? flying) => new()
    {
        RunnerSelected = 0,
        Runners = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                    Label = label,
                    State = flying is null ? RunnerStates.Idle : RunnerStates.Busy,
                    CurrentFlightNumber = flying,
                },
            ],
        },
    };

    [Test]
    public async Task An_idle_runner_gets_one_sentence()
    {
        var said = RunnerDetails.LogAbsence(Fleet("vmlinux001", flying: null));

        await Assert.That(said).StartsWith("No log is available when idle.")
            .Because("an idle runner has no log to fetch and nothing to watch, so everything "
                   + "after that first sentence is a paragraph about something that is not "
                   + "happening. Said: " + said);

        await Assert.That(said.Split('\n').Count(l => l.TrimStart().StartsWith("ssh ")))
            .IsEqualTo(1)
            .Because("one command a person can run, not a menu. Said: " + said);
    }

    [Test]
    public async Task Nothing_left_in_it_says_a_flight_must_be_opened_to_be_watched()
    {
        // THE STALE THIRD. Watching stopped requiring `--attended`, and this
        // pane went on saying it did - which is prose asserting a rule the code
        // no longer has, in the one place a person reads about the feature.
        foreach (var state in new[]
                 {
                     Fleet("vmlinux001", flying: null),
                     Fleet("vmlinux001", flying: "GG-81"),
                     Fleet("vmlinux001:maintain", flying: null),
                 })
        {
            var said = RunnerDetails.LogAbsence(state);

            await Assert.That(said.Contains("opened to be watched", StringComparison.Ordinal))
                .IsFalse()
                .Because("any flight on a runner you registered can be watched now. Said: "
                       + said);
        }
    }

    [Test]
    public async Task A_flying_runner_is_told_which_key_watches_it()
    {
        // AND THE ONE THING THAT IS ACTIONABLE STAYS. `w` is bound while it
        // flies, and this pane is where somebody who has not learned the key
        // finds it.
        var said = RunnerDetails.LogAbsence(Fleet("vmlinux001", flying: "GG-81"));

        await Assert.That(said).Contains("GG-81");
        await Assert.That(said).Contains("`w`")
            .Because("the letter is read off the keymap so the two cannot drift. Said: "
                   + said);
    }

    [Test]
    public async Task The_suggestion_is_still_offered_and_still_says_it_is_a_guess()
    {
        // S34.2-01, UNCHANGED. The modal names where the output is and a
        // person's own access is the channel; what gg knows is a convention it
        // follows itself, not a fact the control plane reported.
        var said = RunnerDetails.LogAbsence(Fleet("vmlinux001:maintain", flying: null));

        await Assert.That(said).Contains("journalctl -u gg-runner-maintain");
        await Assert.That(said.Contains("Suggested", StringComparison.OrdinalIgnoreCase))
            .IsTrue()
            .Because("gg is guessing about somebody else's machine and has to say so. Said: "
                   + said);
    }
}
