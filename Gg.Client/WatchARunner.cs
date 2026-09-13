using System.Security.Cryptography;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>Why a person could not watch a runner, or what they saw.</summary>
/// <remarks>
/// <b>Every one of these sends somebody somewhere different</b>, which is the
/// reason there is a kind rather than a string. A runner that is not beating is
/// a machine; one flying nothing is a queue; one flying something and answering
/// nothing is a machine again — an older gg, or one started without a key —
/// because ANY flight on a runner you registered can be watched now.
/// </remarks>
public enum WatchOutcome
{
    /// <summary>Here is what it is saying.</summary>
    Watching,

    /// <summary>No runner by that id, in this tenant.</summary>
    NoSuchRunner,

    /// <summary>It is not beating.</summary>
    Offline,

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
/// <b>It asks the fleet first, and that is not an optimisation.</b> A runner
/// that is not beating cannot be reached however correct everything else is -
/// an introduction is picked up on a heartbeat, so nothing ever collects the
/// offer - and finding that out by minting a key, sealing an offer and waiting
/// out its whole minute is a sentence about a network for a machine that is
/// simply off. The fleet read is also where the label comes from, so what is
/// said names the machine a person meant rather than a uuid.
/// <para>
/// <b>Flying nothing is no longer one of those refusals.</b> It was, and the
/// reason was true: a channel existed only while a flight did. Which made the
/// one moment a person most wants to be attached - before work arrives - the
/// one moment they could not be.
/// </para>
/// </remarks>
public sealed class WatchARunner(ControlPlaneClient control, ConsoleChannel channel)
{
    /// <summary>What this reach asks a capability to authorise.</summary>
    /// <remarks>
    /// <b>Named here rather than written at the call site</b>, because it is a
    /// decision rather than an argument: an introduction minted to read a log
    /// must not also place a credential, and a literal in the middle of a
    /// method is a decision nothing can assert about.
    /// </remarks>
    public static string Purpose => RunnerCapabilityPurposes.TailYourOwnLog;

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

        // AND FLYING NOTHING IS NO LONGER A REFUSAL. It was, on the grounds
        // that a channel to a runner exists only while a flight does - which
        // made the one moment a person most wants to be attached, before work
        // arrives, the one moment they could not be. A runner answers while it
        // is beating now, so the check above is the whole of what the fleet
        // read is still for.

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        // THE CONTROL PLANE'S WHOLE PART IN THIS: it says which runner, that you
        // may reach it, and for how long. Nothing that passes afterwards is
        // readable by it, which is worth a person seeing named as its own step.
        say($"asking the control plane to introduce you to {runner.Label}");

        var introduced = await control.IntroduceRunnerAsync(
            sessionToken,
            runner.RunnerId,
            Convert.ToBase64String(ephemeral.ExportSubjectPublicKeyInfo()),
            Purpose,
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
            // flying something - which used to mean "a flight nobody launched
            // attended" and now means the machine, because any flight on a
            // runner you registered can be watched.
            var why = reached.Failure is ReachFailure.RunnerNeverAnswered
                ? reached.Said
                + $" {runner.Label} is flying {runner.CurrentFlightNumber}, so it is not away. "
                + "Any flight on a runner you registered can be watched, so this is the "
                + "machine rather than the flight: either it is running a gg too old to "
                + "answer, or it was started without a key and cannot be reached by hand at "
                + "all - `gg runners` says when it was last heard from."
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
                await FollowAsync(
                    (ask, token) => conversation.AskAsync(ask, Patience, token),
                    (span, token) => Task.Delay(span, token),
                    tail.Lines,
                    lines,
                    write,
                    cancellationToken);
            }

            return new Watched(
                WatchOutcome.Watching,
                runner.CurrentFlightNumber is { Length: > 0 } flying
                    ? $"{runner.Label}, flying {flying}"
                    : $"{runner.Label}, waiting for work",
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
    /// <summary>How long one ask is waited for.</summary>
    /// <remarks>
    /// <b>Generous, because the far end may be mid-flight.</b> A runner
    /// answering a tail is reading a file while an agent writes it, and the
    /// cost of being impatient is a silence this side would have to interpret.
    /// </remarks>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>How often the machine itself is asked about, in ticks.</summary>
    /// <remarks>
    /// <b>Beside the tail rather than instead of it.</b> Every tick asks for
    /// the log; this says how often it also asks the runner what it is doing,
    /// which is how a person learns a flight started, learns one landed, and
    /// sees that the machine is still beating while it waits. Five seconds is
    /// slower than the log and far faster than a heartbeat, so nothing is
    /// reported late and nothing is asked for twice between beats.
    /// </remarks>
    private const int StatusEvery = 5;

    /// <summary>How many silences in a row end a watch.</summary>
    /// <remarks>
    /// <b>One used to, and one is what a timeout produces.</b> A null answer
    /// meant "the flight landed and the lease went with it", which was the
    /// ordinary end while a watch lasted a flight. A watch now sits on a machine
    /// that may be idle for an hour, and ending it on a single fifteen second
    /// timeout would be a watch that quietly stopped.
    /// </remarks>
    private const int SilencesThatEndIt = 3;

    /// <summary>
    /// Keeps asking, and writes only what is new.
    /// </summary>
    /// <remarks>
    /// <b>Public because the asking and the waiting are handed in.</b> This was
    /// a private method over a live Conversation and a hard-coded one second
    /// delay, which is why the only part of it a test could reach was the
    /// overlap finder, by reflection - and why when it gives up was asserted
    /// nowhere.
    /// </remarks>
    public static async Task FollowAsync(
        Func<RunnerAsk, CancellationToken, Task<RunnerSaid?>> ask,
        Func<TimeSpan, CancellationToken, Task> wait,
        IReadOnlyList<string> seen,
        int lines,
        Action<string> write,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(write);

        var previous = seen;
        string? flying = null;
        DateTimeOffset? beat = null;
        var silences = 0;
        var tick = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await wait(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // THE MACHINE, PERIODICALLY. What it answers is used to NAME what
            // is happening and to reset the overlap at a flight boundary - it
            // is never what decides whether to ask for the log. A console
            // watching an older runner gets no flight name at all, and gating
            // the log on one would leave it saying "idle" while the runner
            // flies: silently absent, which is indistinguishable from
            // satisfied.
            if (tick++ % StatusEvery == 0)
            {
                // THE PAYLOAD, EMPTY AS IT IS. AskDispatch matches the kind AND
                // the member beside it; a StatusAsk carries nothing and is
                // still what makes this a status ask rather than one the runner
                // does not recognise.
                var said = await ask(
                    new RunnerAsk { Kind = RunnerAskKinds.Status, Status = new StatusAsk() },
                    cancellationToken);

                if (said?.Status is { } status)
                {
                    if (status.BeatAt is { } beatAt && beatAt != beat)
                    {
                        beat = beatAt;

                        // ONLY WHILE THERE IS NOTHING ELSE TO SHOW. A beat line
                        // between an agent's own sentences is noise; a beat line
                        // on a machine that is waiting is the only thing saying
                        // it is still there.
                        if (status.FlightNumber is not { Length: > 0 })
                        {
                            write($"  ... beat {beatAt:HH:mm:ss} - {status.Doing}");
                        }
                    }

                    if (!string.Equals(status.FlightNumber, flying, StringComparison.Ordinal))
                    {
                        if (status.FlightNumber is { Length: > 0 } started)
                        {
                            write($"  ... {started} started here");
                        }
                        else if (flying is { Length: > 0 } landed)
                        {
                            write($"  ... {landed} is no longer flying here");
                        }

                        flying = status.FlightNumber;

                        // AND THE OVERLAP STARTS AGAIN. Every live view opens
                        // with near-identical setup lines, so a match across a
                        // boundary would swallow the new flight's first words -
                        // and no match at all would warn about a gap that did
                        // not happen.
                        previous = [];
                    }
                }
            }

            var tail = await ask(
                new RunnerAsk
                {
                    Kind = RunnerAskKinds.TailLog,
                    TailLog = new TailLogAsk { Lines = lines },
                },
                cancellationToken);

            if (tail?.Tail is not { } said_)
            {
                // A CHANNEL THAT IS GONE, once it has been quiet enough times
                // to mean it. The runner is reachable across flights now, so a
                // single unanswered ask is a hiccup rather than an ending.
                if (++silences >= SilencesThatEndIt)
                {
                    write(
                        $"  ... {SilencesThatEndIt} asks went unanswered, so this channel is "
                      + "gone. Press the key again to open another.");
                    return;
                }

                continue;
            }

            silences = 0;

            var overlap = OverlapOf(previous, said_.Lines);

            if (overlap == 0 && previous.Count > 0 && said_.Lines.Count > 0)
            {
                write(
                    "  ... (more was said than a tail holds, so some of it was missed - "
                  + "ask for more lines to widen the window)");
            }

            foreach (var line in said_.Lines.Skip(overlap))
            {
                write(line);
            }

            previous = said_.Lines;
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
            Patience,
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
