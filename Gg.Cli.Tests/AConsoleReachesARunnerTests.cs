using System.Buffers.Binary;
using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// A console asks a runner about itself, and gets an answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first test in which both halves are present.</b> Everything before it
/// asserted one side against a fixture; this runs the console's offerer and the
/// runner's answerer against each other, through the same seal, the same
/// dispatch and the same closed vocabulary. It lives in <c>Gg.Cli.Tests</c>
/// because that is the only assembly that sees both — which is itself the
/// architecture working: <c>Gg.Console</c> cannot reference <c>Gg.Runner</c>
/// without becoming able to act as one.
/// </para>
/// <para>
/// <b>Serialised, because six real handshakes at once is not what this
/// measures.</b> Each test stands up two peer connections and drives ICE, DTLS
/// and SCTP through to an open data channel; six of those in parallel is twelve
/// peers contending for a two-core CI runner, and one of them took longer than
/// the twenty-second patience and reported <c>NoRouteBetweenUs</c> — a sentence
/// about the network, produced by a shortage of CPU. The class passed four runs
/// locally on a fast machine and failed once on CI, which is the shape of a test
/// that passes because the machine was quick rather than because the code is
/// right. Raising the patience would have hidden it and made the deadline stop
/// meaning what it says.
/// </para>
/// <para>
/// <b>The relay is two delegates rather than a control plane.</b> What the
/// control plane does here is hold a sealed blob until the other end collects
/// it — which is a function, and standing up a database to prove that would test
/// the database. The real relay is asserted separately, in the control plane,
/// including that it cannot read what it carries.
/// </para>
/// </remarks>
[NotInParallel("a-real-webrtc-handshake")]
public class AConsoleReachesARunnerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);

    private sealed class ALog(params string[] lines) : IReadOnlyLog
    {
        public TailRead Tail(int howMany) =>
            new(lines.TakeLast(howMany).ToArray(), lines.Length > howMany);
    }

    private static PinnedRunnerKeys FreshPins() =>
        new(Path.Combine(
            Directory.CreateTempSubdirectory("gg-reach-").FullName, "pinned-runner-keys.json"));

    /// <summary>
    /// Runs the whole handshake with the control plane's part played by a
    /// variable, and hands back what the console got.
    /// </summary>
    private static async Task<(Reached Reached, HandshakeResult Answered, RunnerSealedOffer? Left)>
        HandshakeAsync(
            IReadOnlyLog log,
            PinnedRunnerKeys? pins = null,
            string? pretendKeyIs = null,
            ECDiffieHellman? consoleKey = null,
            TimeSpan? arrivalBound = null)
    {
        using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var ownKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var ephemeral = consoleKey ?? ownKey;

        var runnerPublic = pretendKeyIs
            ?? Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo());

        var introduction = new RunnerIntroduction
        {
            IntroductionId = "intro-1",
            RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
            RunnerPublicKey = runnerPublic,
            Capability = "a-capability",
            ExpiresAt = T0.AddMinutes(1),
        };

        RunnerSealedOffer? left = null;
        HandshakeResult answered = new(null, HandshakeFailure.None, "not run", []);

        var console = new ConsoleChannel([], TimeSpan.FromSeconds(20));
        var runner = new RunnerChannel([], TimeSpan.FromSeconds(20), arrivalBound);

        var reached = await console.ReachAsync(
            introduction,
            ephemeral,
            pins ?? FreshPins(),
            T0,
            async (offer, ct) =>
            {
                left = offer;

                // THE RUNNER'S HALF, RUN WHERE THE CONTROL PLANE WOULD HAVE
                // DELIVERED IT. Answering inside the leave is what a heartbeat
                // does a second later.
                var says = new WhatThisRunnerSays(new SilentObserver(), _ => log, () => T0);

                // IN THE AIR, because the tail follows the flight. A runner
                // that has claimed nothing answers an empty tail whatever log
                // it was built over - which is the right answer for an idle
                // machine and not what a test about carrying lines is asking.
                says.Claimed(Flying());

                answered = await runner.AnswerAsync(
                    new PendingIntroduction { IntroductionId = "intro-1", Offer = offer },
                    runnerKey,
                    new AskDispatch(says),
                    ct);
            },
            _ => Task.FromResult(answered.Answer is { } a
                ? new Collected(a, AnswerState.Arrived)
                : Collected.NotYet),
            cancellationToken: CancellationToken.None);

        return (reached, answered, left);
    }

    /// <summary>A flight to be on, so there is a log to read.</summary>
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
        ExpiresAt = T0.AddMinutes(30),
        RenewWithinSeconds = 30,
    };

    [Test]
    public async Task The_runner_knows_the_console_arrived()
    {
        // THE DIAGNOSTIC HAS TO BE RIGHT ABOUT SUCCESS, and this one was wrong
        // about every success. On the fleet on 2026-09-09, two attended flights
        // carried an agent's own output to a laptop, and the runner wrote
        // "answered and nobody arrived: ChannelNeverOpened. A route was found
        // and no channel opened on it, which is this end" for both - a minute
        // after the conversation had already happened.
        //
        // A DIAGNOSTIC THAT CRIES WOLF ON SUCCESS IS WORSE THAN NONE: it points
        // whoever reads the journal at the end that worked, which is exactly
        // the wrong-end problem this whole path keeps having.
        //
        // Short bound so the wrong answer arrives in seconds rather than in the
        // minute a real runner allows.
        var (reached, answered, _) = await HandshakeAsync(
            new ALog("first", "second"), arrivalBound: TimeSpan.FromSeconds(3));

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.None).Because(reached.Said);

        using var conversation = reached.Conversation!;

        // ASKED AND ANSWERED FIRST, so "the channel opened" is not an opinion:
        // a reply came back over it. Whatever the runner then says about
        // arrival is being said about a channel that demonstrably worked.
        var said = await conversation.AskAsync(
            new RunnerAsk
            {
                Kind = RunnerAskKinds.TailLog,
                TailLog = new TailLogAsk { Lines = 1 },
            },
            TimeSpan.FromSeconds(10));

        await Assert.That(said?.Tail?.Lines).IsEquivalentTo(new[] { "second" });

        await Assert.That(await answered.Serving!.Opened).IsEqualTo(HandshakeFailure.None)
            .Because("a message came back over this channel, so the runner cannot be allowed "
                   + "to report that nobody arrived on it.");
    }

    [Test]
    public async Task A_console_asks_for_a_tail_and_the_runner_answers_it()
    {
        var (reached, _, _) = await HandshakeAsync(new ALog("first", "second", "third"));

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.None)
            .Because(reached.Said);

        using var conversation = reached.Conversation!;

        var said = await conversation.AskAsync(
            new RunnerAsk
            {
                Kind = RunnerAskKinds.TailLog,
                TailLog = new TailLogAsk { Lines = 2 },
            },
            TimeSpan.FromSeconds(10));

        await Assert.That(said!.Tail!.Lines).IsEquivalentTo(new[] { "second", "third" })
            .Because("this is the whole feature: a person asks a machine what it is doing and "
                   + "the control plane never sees the answer.");
        await Assert.That(said.Tail.Truncated).IsTrue();
    }

    [Test]
    public async Task It_answers_a_status_over_the_same_channel()
    {
        var (reached, _, _) = await HandshakeAsync(new ALog());

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.None).Because(reached.Said);

        using var conversation = reached.Conversation!;

        var said = await conversation.AskAsync(
            new RunnerAsk { Kind = RunnerAskKinds.Status, Status = new StatusAsk() },
            TimeSpan.FromSeconds(10));

        await Assert.That(said!.Status!.Doing).IsNotEmpty();
        await Assert.That(said.Status.At).IsEqualTo(T0);
    }

    [Test]
    public async Task A_kind_outside_the_vocabulary_gets_no_answer_over_a_real_channel()
    {
        // THE CLOSED CHANNEL, END TO END. The dispatch is unit tested; this is
        // the same refusal with a DTLS association and an SCTP stream under it,
        // which is where somebody would expect a general channel to leak.
        var (reached, _, _) = await HandshakeAsync(new ALog("a line"));

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.None).Because(reached.Said);

        using var conversation = reached.Conversation!;

        var said = await conversation.AskAsync(
            new RunnerAsk { Kind = "run-command" }, TimeSpan.FromSeconds(3));

        await Assert.That(said).IsNull()
            .Because("a runner that answered this would have a channel wider than the "
                   + "vocabulary says it has.");
    }

    [Test]
    public async Task Nothing_is_sent_when_the_key_is_not_the_one_this_console_pinned()
    {
        // THE PIN, AND THE ORDER IT IS CHECKED IN. Sealing first and noticing
        // after would have sent the offer - candidates and all - to whoever
        // substituted the key.
        var pins = FreshPins();
        pins.Check("01a06385-322f-7371-93a2-ce35db5c4fbe", "the-key-we-met-first", T0);

        using var impostor = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var (reached, answered, _) = await HandshakeAsync(
            new ALog("a line"),
            pins,
            Convert.ToBase64String(impostor.ExportSubjectPublicKeyInfo()));

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.KeyChanged);
        await Assert.That(answered.Said).IsEqualTo("not run")
            .Because("the runner's half must never have been reached: nothing was sealed, so "
                   + "nothing was left anywhere.");
        await Assert.That(reached.Said).Contains("gg runner repin")
            .Because("a refusal that names no way through is a dead end, and a reinstall is "
                   + "the common cause.");
    }

    [Test]
    public async Task It_seals_with_the_key_the_introduction_was_minted_with()
    {
        // WHAT THE CONTROL PLANE ALREADY WRITES DOWN. Minting an introduction
        // sends the console's ephemeral public key and the control plane stores
        // its hash against the row. A console that seals with a key it made
        // afterwards has declared one thing and done another - which compiles,
        // works today, and is refused the moment anything checks the binding it
        // is already keeping.
        using var declared = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var (reached, _, left) = await HandshakeAsync(new ALog("a line"), consoleKey: declared);

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.None).Because(reached.Said);
        using var conversation = reached.Conversation!;

        await Assert.That(left).IsNotNull();

        // READ OUT OF THE FRAME, the way the runner reads it and the way the
        // control plane could: the ephemeral key is length-prefixed ahead of the
        // ciphertext, so proving this needs nothing the seal protects.
        var keyLength = BinaryPrimitives.ReadInt32BigEndian(left!.Sealed);
        var sealedKey = Convert.ToBase64String(left.Sealed.AsSpan(4, keyLength));

        await Assert.That(sealedKey)
            .IsEqualTo(Convert.ToBase64String(declared.ExportSubjectPublicKeyInfo()))
            .Because("the offer has to be sealed with the key the introduction was minted "
                   + "with, or the hash the control plane stored is about nothing.");
    }

    [Test]
    public async Task An_introduction_that_ended_is_not_reported_as_a_silent_runner()
    {
        // WAITING AGAINST WRONG, at the far end of the chain that carries it.
        // The control plane answers 404 for an introduction that expired and 204
        // while there is simply no answer yet, and this is the sentence a person
        // reads when it was the first.
        using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var console = new ConsoleChannel([], TimeSpan.FromSeconds(20));

        var started = DateTimeOffset.UtcNow;

        var reached = await console.ReachAsync(
            new RunnerIntroduction
            {
                IntroductionId = "intro-1",
                RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                RunnerPublicKey = Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo()),
                Capability = "a-capability",
                ExpiresAt = T0.AddMinutes(1),
            },
            ephemeral,
            FreshPins(),
            T0,
            (_, _) => Task.CompletedTask,
            _ => Task.FromResult(Collected.Gone),
            cancellationToken: CancellationToken.None);

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.IntroductionExpired);
        await Assert.That(reached.Said).Contains("not a network")
            .Because("the runner is the wrong place to send somebody for this, and a sentence "
                   + "about a machine is what sends them there.");

        await Assert.That(DateTimeOffset.UtcNow - started).IsLessThan(TimeSpan.FromSeconds(15))
            .Because("an introduction that is over will not come back, so asking for the rest "
                   + "of the patience turns a fact into a wait.");
    }

    [Test]
    public async Task The_console_waits_as_long_as_the_introduction_lasts()
    {
        // THE NUMBER THE CONSOLE ALREADY HAS AND DID NOT USE. An introduction
        // carries ExpiresAt - "when it stops working", the control plane's own
        // statement - and the answer wait was a constructor argument instead:
        // twenty seconds against an introduction that lasts a minute.
        //
        // Twenty was never enough. An introduction is picked up on a heartbeat,
        // and the ordinary interval is a third of the staleness bound - fifteen
        // seconds on the deployed control plane - so the worst case is fifteen
        // plus a handshake, against a console that stopped asking at twenty.
        // Measured on the fleet: the offer was left at 23:18:58.9, the console
        // polled 62 times over 19.8s and gave up, and the sentence it printed
        // blamed the machine.
        using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        // A SHORT LOCAL PATIENCE and a longer introduction, so the two cannot be
        // confused: gathering candidates and opening a channel are this
        // machine's business, and how long a runner has to answer is not.
        var console = new ConsoleChannel([], TimeSpan.FromSeconds(1));

        var asked = new List<DateTimeOffset>();

        var reached = await console.ReachAsync(
            new RunnerIntroduction
            {
                IntroductionId = "intro-1",
                RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                RunnerPublicKey = Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo()),
                Capability = "a-capability",
                ExpiresAt = T0.AddSeconds(4),
            },
            ephemeral,
            FreshPins(),
            T0,
            (_, _) => Task.CompletedTask,
            _ =>
            {
                asked.Add(DateTimeOffset.UtcNow);
                return Task.FromResult(Collected.NotYet);
            },
            cancellationToken: CancellationToken.None);

        await Assert.That(asked).IsNotEmpty();

        // MEASURED FROM THE FIRST ASK, not from the call, so the time spent
        // gathering candidates is not counted as time spent waiting.
        await Assert.That(asked[^1] - asked[0]).IsGreaterThan(TimeSpan.FromSeconds(2))
            .Because("the console has to still be asking when the runner's next beat comes "
                   + "round, and giving up first turns a wait into a sentence about a "
                   + $"machine. Asked {asked.Count} times over "
                   + $"{(asked[^1] - asked[0]).TotalSeconds:0.0}s.");

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.RunnerNeverAnswered);
    }

    [Test]
    public async Task A_runner_that_never_answers_says_so_rather_than_hanging()
    {
        using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var neverUsed = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var console = new ConsoleChannel([], TimeSpan.FromSeconds(2));

        var reached = await console.ReachAsync(
            new RunnerIntroduction
            {
                IntroductionId = "intro-1",
                RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                RunnerPublicKey = Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo()),
                Capability = "a-capability",
                // SHORT ON PURPOSE. The subject is the sentence, not the wait -
                // and the wait is the introduction's own life now, so a minute
                // here would be a minute of test.
                ExpiresAt = T0.AddSeconds(1),
            },
            neverUsed,
            FreshPins(),
            T0,
            (_, _) => Task.CompletedTask,
            _ => Task.FromResult(Collected.NotYet),
            cancellationToken: CancellationToken.None);

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.RunnerNeverAnswered);
        // THE CAUSES CHANGED WITH THE WAIT. "Between heartbeats" used to be the
        // cause that resolves itself, and it was the honest one while the
        // console gave up after twenty seconds against a minute-long
        // introduction. Now the console waits the whole life of it, so a beat
        // that was merely due is no longer an explanation - what is left is a
        // machine that is not beating at all, and the sentence has to say where
        // to look rather than list a possibility that can no longer happen.
        await Assert.That(reached.Said).Contains("not beating")
            .Because("the sentence has to name causes a person can act on, and it must not "
                   + "keep offering one the code has ruled out.");

        await Assert.That(reached.Said).Contains("gg runners")
            .Because("naming a cause without naming where to check it leaves a person with "
                   + "a diagnosis and nowhere to take it.");
    }
}
