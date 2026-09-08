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
/// <b>The relay is two delegates rather than a control plane.</b> What the
/// control plane does here is hold a sealed blob until the other end collects
/// it — which is a function, and standing up a database to prove that would test
/// the database. The real relay is asserted separately, in the control plane,
/// including that it cannot read what it carries.
/// </para>
/// </remarks>
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
            ECDiffieHellman? consoleKey = null)
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
        var runner = new RunnerChannel([], TimeSpan.FromSeconds(20));

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
                answered = await runner.AnswerAsync(
                    new PendingIntroduction { IntroductionId = "intro-1", Offer = offer },
                    runnerKey,
                    new AskDispatch(new WhatThisRunnerSays(new SilentObserver(), log, () => T0)),
                    ct);
            },
            _ => Task.FromResult(answered.Answer),
            CancellationToken.None);

        return (reached, answered, left);
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
                ExpiresAt = T0.AddMinutes(1),
            },
            neverUsed,
            FreshPins(),
            T0,
            (_, _) => Task.CompletedTask,
            _ => Task.FromResult<RunnerSealedAnswer?>(null),
            CancellationToken.None);

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.RunnerNeverAnswered);
        await Assert.That(reached.Said).Contains("offline")
            .Because("the sentence has to name the causes a person can check, and a runner "
                   + "between heartbeats is the one that resolves itself.");
    }
}
