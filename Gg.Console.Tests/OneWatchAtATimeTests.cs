using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The console holds one watched runner, and the pane reads its buffer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pump is <c>WatchARunner</c> itself.</b> Following is already "ask
/// again and write only what is new", and the thing it writes to is an
/// <c>Action&lt;string&gt;</c> — so pointing that at a buffer is the whole of
/// it. Nothing here re-implements polling, dedupe or pacing, because a second
/// copy of those is a second set of answers to how fast a runner is asked.
/// </para>
/// <para>
/// <b>One at a time, and that is a decision rather than a limitation.</b> There
/// is one live pane. Two conversations feeding it would interleave two flights'
/// output into one box with nothing saying which line came from where — and the
/// second runner's pump would go on asking after nobody could see it.
/// </para>
/// <para>
/// <b>It is not a session source and must never become one.</b> It holds the
/// conversation; the buffer it fills is what a session touches. The two live in
/// separate files so that split is something a scan can check rather than
/// something a comment claims.
/// </para>
/// </remarks>
public class OneWatchAtATimeTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 9, 1, 45, 0, TimeSpan.Zero);

    /// <summary>A follow that hands over what it is told to, then waits to be stopped.</summary>
    private static Func<string, Action<string>, Action<string>, Action, CancellationToken,
        Task<string>> Following(params string[] lines) =>
        async (_, onLine, _, onOpen, token) =>
        {
            // OPENED FIRST, because that is the order the real one has: the
            // channel answers an ask before anything is written.
            onOpen();

            foreach (var line in lines)
            {
                onLine(line);
            }

            await Task.Delay(Timeout.Infinite, token);
            return "";
        };

    [Test]
    public async Task What_the_runner_says_reaches_the_pane_for_that_flight()
    {
        using var watched = new WatchedRunner(Following("one", "two"), () => T0);

        watched.Start("a-runner", "a-flight");

        var source = watched.SourceFor("a-flight");

        await Assert.That(source).IsNotNull()
            .Because("the pane is keyed by flight, so a watch that registered under anything "
                   + "else is a watch nothing draws.");

        // BOUNDED POLL rather than a sleep: the follow runs on its own thread,
        // and asserting after a fixed wait is the thing that passes here and
        // fails on a two-core runner.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        IReadOnlyList<StreamLine> got = [];

        while (got.Count < 2 && DateTime.UtcNow < deadline)
        {
            foreach (var line in source!.Read())
            {
                got = [.. got, line];
            }

            await Task.Delay(10);
        }

        await Assert.That(got.Select(l => l.Text)).IsEquivalentTo(new[] { "one", "two" });
    }

    [Test]
    public async Task The_connect_narrates_into_the_pane_and_so_does_its_ending()
    {
        // WHERE THE STEPS WERE WANTED. They used to print on the bare terminal
        // the console had just torn itself down to free - which is a screen that
        // stops existing the moment the console comes back, so a connect that
        // took fifteen seconds and failed left nothing to read.
        //
        // AND THE ENDING WITH THEM, because "how far it got" and "why it
        // stopped" are two halves of one account.
        using var watched = new WatchedRunner(
            async (_, _, onStep, _, _) =>
            {
                onStep("asking the control plane to introduce you to vmlinux001");
                onStep("offer left; waiting for the runner to pick it up");
                await Task.CompletedTask;
                return "nobody answered, and here is why";
            },
            () => T0);

        watched.Start("a-runner", "a-flight");

        var source = watched.SourceFor("a-flight")!;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        var seen = new List<StreamLine>();

        while (seen.Count < 3 && DateTime.UtcNow < deadline)
        {
            seen.AddRange(source.Read());
            await Task.Delay(10);
        }

        var whole = string.Join(" | ", seen.Select(l => l.Text));

        await Assert.That(whole).Contains("introduce you");
        await Assert.That(whole).Contains("offer left");
        await Assert.That(whole).Contains("nobody answered")
            .Because("the reason belongs beside the steps that led to it, in the pane a "
                   + "person is looking at. Seen: " + whole);

        await Assert.That(seen.All(l => l.Kind == StreamLineKind.Setup)).IsTrue()
            .Because("they are setup rather than the agent's own words, and the pane draws "
                   + "the two differently.");
    }

    [Test]
    public async Task A_flight_nobody_is_watching_has_no_source()
    {
        using var watched = new WatchedRunner(Following(), () => T0);

        watched.Start("a-runner", "a-flight");

        await Assert.That(watched.SourceFor("some-other-flight")).IsNull()
            .Because("the composition root falls back to the file for anything this does not "
                   + "answer for, so answering for a flight nobody is watching would put an "
                   + "empty remote buffer in front of a local tail that has lines in it.");
    }

    [Test]
    public async Task Watching_a_second_runner_stops_the_first()
    {
        // ONE PANE. Two conversations feeding it would interleave two flights
        // into one box with nothing saying which line came from where, and the
        // first runner's pump would go on asking after nobody could see it.
        var stopped = new TaskCompletionSource();

        using var watched = new WatchedRunner(
            async (runner, _, _, onOpen, token) =>
            {
                if (runner == "first")
                {
                    token.Register(stopped.SetResult);
                }

                onOpen();

                await Task.Delay(Timeout.Infinite, token);
                return "";
            },
            () => T0);

        watched.Start("first", "first-flight");
        watched.Start("second", "second-flight");

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(watched.SourceFor("first-flight")).IsNull();
        await Assert.That(watched.SourceFor("second-flight")).IsNotNull();
    }

    [Test]
    public async Task Stopping_says_so_on_the_pane_rather_than_going_quiet()
    {
        // A CHANNEL THAT CLOSED IS NOT A CHANNEL WITH NOTHING NEW ON IT. The
        // pane draws a different silence for each, and a watch that ended
        // without saying so leaves the box that means "the agent is working"
        // over a conversation that is over.
        using var watched = new WatchedRunner(Following(), () => T0);

        watched.Start("a-runner", "a-flight");

        var source = watched.SourceFor("a-flight")!;

        // OPEN ONLY ONCE THERE IS A CHANNEL. Start returns before there is one,
        // which is the whole reason Opened exists.
        await Assert.That(watched.Opened(TimeSpan.FromSeconds(10)).Open).IsTrue();
        await Assert.That(source.Exists).IsTrue();

        watched.Stop();

        await Assert.That(source.Exists).IsFalse();
        await Assert.That(watched.SourceFor("a-flight")).IsNull();
    }

    [Test]
    public async Task The_holder_reaches_no_pane_and_no_terminal()
    {
        // THE OTHER HALF OF THE SPLIT. RemoteLiveSource may not name a
        // conversation; this may not name a view. Between them the session
        // touches a buffer and nothing else, which is the whole argument for
        // why watching a fleet runner inline costs no exception.
        var source = Sources.Read("Gg.Console", "WatchedRunner.cs");

        foreach (var forbidden in new[] { "AppState", "Terminal", "View", "Application" })
        {
            await Assert.That(source.Contains(forbidden, StringComparison.Ordinal)).IsFalse()
                .Because($"the half that holds the conversation may not name {forbidden}.");
        }
    }
}
