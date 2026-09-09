using System.Security.Cryptography;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>Why a person could not watch a runner, or what they saw.</summary>
/// <remarks>
/// <b>Every one of these sends somebody somewhere different</b>, which is the
/// reason there is a kind rather than a string. A runner that is not beating is
/// a machine; one flying nothing is a queue; one flying something headless is a
/// flight that was launched without <c>--attended</c> and cannot be reached
/// however healthy it is.
/// </remarks>
public enum WatchOutcome
{
    /// <summary>Here is what it is saying.</summary>
    Watching,

    /// <summary>No runner by that id, in this tenant.</summary>
    NoSuchRunner,

    /// <summary>It is not beating.</summary>
    Offline,

    /// <summary>It is beating and flying nothing.</summary>
    FlyingNothing,

    /// <summary>The control plane would not introduce this person to it.</summary>
    NotIntroduced,

    /// <summary>It was introduced and could not be reached.</summary>
    NotReached,
}

/// <summary>What watching a runner produced.</summary>
public sealed record Watched(
    WatchOutcome Outcome, string Said, IReadOnlyList<string> Lines, bool Truncated);

/// <summary>
/// Watching one runner: introduce, reach, ask, print.
/// </summary>
/// <remarks>
/// <para>
/// <b>The entry point everything else was machinery for.</b> The offerer, the
/// seal, the relay and the runner's session all existed and nothing called them;
/// a person opening the console found a suggested <c>ssh</c> command and no way
/// to use any of it.
/// </para>
/// <para>
/// <b>It asks the fleet first, and that is not an optimisation.</b> A channel
/// exists only while an attended flight is flying, so a runner that is offline
/// or idle cannot be reached however correct everything else is — and reaching
/// anyway would spend twenty seconds and then say "the runner did not answer",
/// which reads as a broken machine. The fleet read already carries state and the
/// current flight; using it is the difference between a diagnosis and a timeout.
/// </para>
/// </remarks>
public sealed class WatchARunner(ControlPlaneClient control, ConsoleChannel channel)
{
    public async Task<Watched> WatchAsync(
        string sessionToken,
        string runnerId,
        PinnedRunnerKeys pins,
        int lines,
        DateTimeOffset now,
        Action<string> write,
        bool follow = true,
        // WHAT IT IS DOING WHILE IT DOES IT, kept apart from `write` because
        // the two have different audiences and, in a console, different places
        // to land: this is the connect and that is the agent's own words. A
        // caller wanting none passes none.
        Action<string>? saying = null,
        // SAID ONCE, WHEN THERE IS A CHANNEL AND IT HAS ANSWERED. A caller
        // deciding whether to put a pane in front of somebody needs the
        // difference between "still connecting" and "connected", and reading
        // that off the last sentence in `saying` would be a caller matching on
        // prose.
        Action? opened = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);

        ArgumentNullException.ThrowIfNull(pins);

        var say = saying ?? (_ => { });

        say("asking the control plane which runners you can see");

        var fleet = await control.ListRunnersAsync(sessionToken, cancellationToken);

        if (fleet.Runners.FirstOrDefault(r =>
                string.Equals(r.RunnerId, runnerId, StringComparison.OrdinalIgnoreCase))
            is not { } runner)
        {
            return Nothing(
                WatchOutcome.NoSuchRunner,
                $"There is no runner {runnerId} here. `gg runners` lists the ones you can see.");
        }

        // ASKED BEFORE ANYTHING IS MINTED. Each of these would otherwise become
        // twenty seconds of silence and a sentence about a machine that is fine.
        if (string.Equals(runner.State, "offline", StringComparison.Ordinal))
        {
            return Nothing(
                WatchOutcome.Offline,
                $"{runner.Label} is not beating, so there is nothing to reach. It was last "
              + $"heard from {Ago(runner.LastHeartbeatAt, now)}.");
        }

        if (runner.CurrentFlightId is not { Length: > 0 })
        {
            return Nothing(
                WatchOutcome.FlyingNothing,
                $"{runner.Label} is beating and flying nothing. There is nothing to watch: a "
              + "channel to a runner exists only while a flight does, which is what stops it "
              + "being a standing way in.");
        }

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        // THE CONTROL PLANE'S WHOLE PART IN THIS: it says which runner, that you
        // may reach it, and for how long. Nothing that passes afterwards is
        // readable by it, which is worth a person seeing named as its own step.
        say($"asking the control plane to introduce you to {runner.Label}");

        var introduced = await control.IntroduceRunnerAsync(
            sessionToken,
            runner.RunnerId,
            Convert.ToBase64String(ephemeral.ExportSubjectPublicKeyInfo()),
            cancellationToken);

        if (introduced.Introduction is not { } introduction)
        {
            return Nothing(WatchOutcome.NotIntroduced, introduced.Said);
        }

        var reached = await channel.ReachAsync(
            introduction,
            ephemeral,
            pins,
            now,
            (offer, token) =>
                control.LeaveOfferAsync(sessionToken, introduction.IntroductionId, offer, token),
            token =>
                control.CollectAnswerAsync(sessionToken, introduction.IntroductionId, token),
            saying,
            cancellationToken);

        if (reached.Conversation is not { } conversation)
        {
            // THE ONE SENTENCE THE CHANNEL CANNOT WRITE FOR ITSELF. ReachAsync
            // knows nobody answered; only here is it known that this runner IS
            // flying something - so the likeliest reason is a flight nobody
            // launched attended, which no amount of network is going to fix.
            var why = reached.Failure is ReachFailure.RunnerNeverAnswered
                ? reached.Said
                + $" {runner.Label} is flying {runner.CurrentFlightNumber}, so it is not away: "
                + "a runner opens a channel only for a flight launched with `--attended`, and "
                + "an ordinary flight cannot be watched however healthy the machine is."
                : reached.Said;

            return Nothing(WatchOutcome.NotReached, why);
        }

        using (conversation)
        {
            // SAID AFTER THE FIRST ASK LANDS, not on the handshake. A channel
            // that opened and then declined to answer is a runner problem, and
            // a caller told "connected" before that was known would put a pane
            // over it and call the silence the agent thinking.
            var first = await AskAsync(conversation, lines, cancellationToken);

            if (first is not { } tail)
            {
                return Nothing(
                    WatchOutcome.NotReached,
                    $"{runner.Label} opened a channel and did not answer the ask. That is this "
                  + "runner rather than the network - the route is there and something on the "
                  + "far side declined to say anything.");
            }

            opened?.Invoke();

            foreach (var line in tail.Lines)
            {
                write(line);
            }

            if (follow)
            {
                await FollowAsync(conversation, tail.Lines, lines, write, cancellationToken);
            }

            return new Watched(
                WatchOutcome.Watching,
                $"{runner.Label}, flying {runner.CurrentFlightNumber}",
                tail.Lines,
                tail.Truncated);
        }
    }

    /// <summary>
    /// Keeps asking, and writes only what is new.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Polled, because the channel carries a request and a bounded response
    /// and nothing else.</b> There is no push shape in <c>RunnerAskKinds</c> and
    /// adding one would be a contract change - so following is asking again, and
    /// the pace is the only thing to get right: fast enough that a person sees a
    /// flight move, slow enough that watching one does not cost the runner an
    /// ask per frame.
    /// </para>
    /// <para>
    /// <b>New lines are found by OVERLAP rather than by counting.</b> A tail is
    /// the last N lines, so two reads share a suffix and a prefix; the longest
    /// match between them is where the old ends. Counting would be wrong the
    /// first time a flight said the same thing twice, and skipping a fixed
    /// number would be wrong whenever the log grew by more than one line.
    /// </para>
    /// <para>
    /// <b>And when there is no overlap at all, it SAYS SO.</b> That means the
    /// flight produced more than a whole tail between two reads, so something
    /// was missed - and a watcher who is not told that believes they saw
    /// everything. Silence is the one answer this cannot give.
    /// </para>
    /// </remarks>
    private static async Task FollowAsync(
        Conversation conversation,
        IReadOnlyList<string> seen,
        int lines,
        Action<string> write,
        CancellationToken cancellationToken)
    {
        var previous = seen;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (await AskAsync(conversation, lines, cancellationToken) is not { } tail)
            {
                // A RUN THAT STOPPED ANSWERING IS THE ORDINARY END of watching:
                // the flight landed and the lease went with it, which closed the
                // channel. Saying nothing here is right - the caller's own
                // ending says what became of the flight.
                return;
            }

            var overlap = OverlapOf(previous, tail.Lines);

            if (overlap == 0 && previous.Count > 0 && tail.Lines.Count > 0)
            {
                write(
                    "  ... (more was said than a tail holds, so some of it was missed - "
                  + "ask for more lines to widen the window)");
            }

            foreach (var line in tail.Lines.Skip(overlap))
            {
                write(line);
            }

            previous = tail.Lines;
        }
    }

    /// <summary>
    /// How many of the new tail's first lines are the old tail's last lines.
    /// </summary>
    private static int OverlapOf(IReadOnlyList<string> before, IReadOnlyList<string> now)
    {
        for (var take = Math.Min(before.Count, now.Count); take > 0; take--)
        {
            var matches = true;

            for (var i = 0; i < take && matches; i++)
            {
                matches = string.Equals(
                    before[before.Count - take + i], now[i], StringComparison.Ordinal);
            }

            if (matches)
            {
                return take;
            }
        }

        return 0;
    }

    private static async Task<LogTail?> AskAsync(
        Conversation conversation, int lines, CancellationToken cancellationToken)
    {
        var said = await conversation.AskAsync(
            new RunnerAsk
            {
                Kind = RunnerAskKinds.TailLog,
                TailLog = new TailLogAsk { Lines = lines },
            },
            TimeSpan.FromSeconds(15),
            cancellationToken);

        return said?.Tail;
    }

    private static Watched Nothing(WatchOutcome outcome, string said) =>
        new(outcome, said, [], false);

    /// <summary>How long ago, in words rather than a timestamp to subtract.</summary>
    private static string Ago(DateTimeOffset? at, DateTimeOffset now) =>
        at is not { } when
            ? "never"
            : (now - when) is var since && since < TimeSpan.FromMinutes(2)
                ? $"{(int)since.TotalSeconds}s ago"
                : since < TimeSpan.FromHours(2)
                    ? $"{(int)since.TotalMinutes}m ago"
                    : $"{(int)since.TotalHours}h ago";
}
