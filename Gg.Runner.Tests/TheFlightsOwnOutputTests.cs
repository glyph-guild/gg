using System.Text;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// What a person watching a remote flight actually reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step 0 said the tail should be <c>journalctl -u &lt;unit&gt;</c>, and
/// looking at the journal says otherwise.</b> On the fleet's only supervised
/// runner it holds that runner's own narration and its crash traces — the unit
/// runs pool maintenance and never flies a flight, so no agent text passes
/// through it at all. What an agent says goes to the live view, as NDJSON,
/// and always has.
/// </para>
/// <para>
/// <b>And the live view is per FLIGHT, which is the security half.</b> A journal
/// belongs to the machine, so tailing one would hand somebody every flight that
/// runner is running, including other people's. The lease authorises one
/// conversation about one flight; naming the file is what makes the narrowing
/// structural rather than a filter somebody remembered to apply.
/// </para>
/// </remarks>
public class TheFlightsOwnOutputTests
{
    private static string AView(params string[] lines)
    {
        var path = Path.Combine(
            Directory.CreateTempSubdirectory("gg-live-").FullName, "flight.ndjson");

        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        return path;
    }

    private static string Entry(string kind, string text) =>
        $$"""{"kind":"{{kind}}","text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"at":"2026-09-08T00:00:00+00:00"}""";

    [Test]
    public async Task It_reads_what_the_agent_said_and_says_which_lines_were_the_agent()
    {
        // THE KIND IS KEPT. A tail rendering only the text would put prose and
        // tool names in one undifferentiated column, and telling those apart is
        // the whole reason somebody is watching.
        var log = new TheFlightsOwnOutput(AView(
            Entry("setup", "session init"),
            Entry("tool", "Bash"),
            Entry("text", "I made no code changes.")));

        var read = log.Tail(10);

        await Assert.That(read.Lines).IsEquivalentTo(new[]
        {
            "setup: session init",
            "tool: Bash",
            "text: I made no code changes.",
        });
        await Assert.That(read.Truncated).IsFalse();
    }

    [Test]
    public async Task It_answers_with_the_NEWEST_lines()
    {
        // A tail is about what is happening NOW. Cutting from the back would
        // answer "what is it doing" with what it did first.
        var log = new TheFlightsOwnOutput(AView(
            Entry("text", "first"), Entry("text", "second"), Entry("text", "third")));

        var read = log.Tail(2);

        await Assert.That(read.Lines).IsEquivalentTo(new[] { "text: second", "text: third" });
        await Assert.That(read.Truncated).IsTrue()
            .Because("there is more than this, and a person deciding whether to ask for more "
                   + "needs to know that rather than infer it from a round number.");
    }

    [Test]
    public async Task A_flight_that_has_not_said_anything_yet_reads_empty_rather_than_broken()
    {
        // A CLAIMED FLIGHT HAS WRITTEN NOTHING. Somebody asking then should be
        // told "nothing so far", not shown an error about a path they cannot
        // see and did not choose.
        var log = new TheFlightsOwnOutput(
            Path.Combine(Directory.CreateTempSubdirectory("gg-live-").FullName, "absent.ndjson"));

        var read = log.Tail(10);

        await Assert.That(read.Lines).IsEmpty();
        await Assert.That(read.Truncated).IsFalse();
    }

    [Test]
    public async Task A_half_written_line_is_shown_rather_than_dropped()
    {
        // THE ORDINARY CASE, not a corruption: the runner appends while this
        // reads, so the last line is sometimes incomplete. The next read has
        // the whole of it.
        var log = new TheFlightsOwnOutput(AView(
            Entry("text", "complete"),
            """{"kind":"text","text":"half a li"""));

        var read = log.Tail(10);

        await Assert.That(read.Lines.Count).IsEqualTo(2);
        await Assert.That(read.Lines[1]).Contains("half a li");
    }

    [Test]
    public async Task The_reader_is_not_confused_by_a_flight_that_says_nothing_but_tools()
    {
        // The liveness half: every assertion above would pass on a reader that
        // only ever recognised `text`, and a flight mid-tool-call is the state
        // somebody is most likely to catch it in.
        var log = new TheFlightsOwnOutput(AView(Entry("tool", "Read"), Entry("tool", "→ ok")));

        await Assert.That(log.Tail(10).Lines).IsEquivalentTo(new[] { "tool: Read", "tool: → ok" });
    }

    /// <summary>
    /// A runner that is flying something, because only one of those has a log.
    /// </summary>
    /// <remarks>
    /// <b>The tail follows the flight now.</b> An object that has claimed
    /// nothing answers an empty tail whatever log it was built over - which is
    /// the answer an idle machine owes a watcher, and is why a test about what a
    /// tail CONTAINS has to put it in the air first.
    /// </remarks>
    private static LeaseGranted Flying() => new()
    {
        LeaseId = "lease-84",
        Generation = 1,
        FlightId = "flight-84",
        FlightNumber = "GG-84",
        Repos = [],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = DateTimeOffset.UnixEpoch.AddMinutes(30),
        RenewWithinSeconds = 30,
    };

    [Test]
    public async Task A_tail_bigger_than_the_contract_allows_is_cut_to_it()
    {
        // RunnerAskBounds.MaxBytes was declared, asserted as a constant, and
        // enforced NOWHERE. It did not matter while no production log was wired;
        // it matters the moment one `text` entry is a paragraph of prose.
        var essay = new string('x', 4096);
        var many = Enumerable.Range(0, 200).Select(_ => Entry("text", essay)).ToArray();

        var says = new WhatThisRunnerSays(
            new SilentObserver(), _ => new TheFlightsOwnOutput(AView(many)), () => DateTimeOffset.UnixEpoch);

        // IN THE AIR, because the tail follows the flight: an object that has
        // claimed nothing answers an empty tail whatever log it was built over.
        says.Claimed(Flying());

        var dispatch = new AskDispatch(says);

        var said = dispatch.Answer(new RunnerAsk
        {
            Kind = RunnerAskKinds.TailLog,
            TailLog = new TailLogAsk { Lines = 200 },
        });

        var bytes = said!.Tail!.Lines.Sum(l => Encoding.UTF8.GetByteCount(l) + 1);

        await Assert.That(bytes).IsLessThanOrEqualTo(RunnerAskBounds.MaxBytes)
            .Because("the bound is on the contract so THIS side can hold it - a bound only the "
                   + "far end enforces disappears the moment the far end is wrong, and this is "
                   + "the machine whose egress it spends.");

        await Assert.That(said.Tail.Truncated).IsTrue();
        await Assert.That(said.Tail.Lines).IsNotEmpty()
            .Because("cutting to nothing would answer a question about a busy flight with "
                   + "silence, which reads as a flight that said nothing.");
    }
}
