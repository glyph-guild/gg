using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// Reaching a runner says what it is doing while it does it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The connect is the part that takes time and the part that said
/// nothing.</b> An offer is picked up on the runner's next heartbeat, and the
/// ordinary interval is a third of the staleness bound — fifteen seconds on the
/// deployed control plane. So the ordinary, healthy case is a console sitting
/// silent for up to fifteen seconds and then printing either a log or a
/// refusal, with nothing in between to say which of five steps it was on.
/// </para>
/// <para>
/// <b>Silence is where this whole path keeps going wrong.</b> A console that
/// waited and gave up said "the machine is not asking" when the machine was
/// fine; a runner that answered said "nobody arrived" while the tail was on
/// somebody's screen. Both were one end reporting a verdict about the other
/// end, and neither would have survived a minute if the steps had been on the
/// screen as they happened.
/// </para>
/// <para>
/// <b>In this class rather than beside <c>ConsoleChannel</c></b>, because these
/// stand up real peer connections and <c>a-real-webrtc-handshake</c> is the key
/// that stops several of them running at once — which is a flake this
/// repository has already paid for on a two-core runner.
/// </para>
/// </remarks>
[NotInParallel("a-real-webrtc-handshake")]
public class TheConnectionSaysWhatItIsDoingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 1, 0, 0, TimeSpan.Zero);

    private const string TheRunner = "01a06385-322f-7371-93a2-ce35db5c4fbe";

    private static PinnedRunnerKeys FreshPins() =>
        new(Path.Combine(
            Directory.CreateTempSubdirectory("gg-saying-").FullName, "pinned-runner-keys.json"));

    private static RunnerIntroduction An(string runnerPublicKey, int lasts = 2) => new()
    {
        IntroductionId = "intro-1",
        RunnerId = TheRunner,
        RunnerPublicKey = runnerPublicKey,
        Capability = "a-capability",
        ExpiresAt = T0.AddSeconds(lasts),
    };

    [Test]
    public async Task It_says_each_step_as_it_reaches_it()
    {
        using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var said = new List<string>();

        // NOBODY ANSWERS, which is the case worth narrating: this is exactly the
        // wait a person needs told apart from a hang.
        _ = await new ConsoleChannel([], TimeSpan.FromSeconds(2)).ReachAsync(
            An(Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo())),
            ephemeral,
            FreshPins(),
            T0,
            (_, _) => Task.CompletedTask,
            _ => Task.FromResult(Collected.NotYet),
            saying: said.Add,
            cancellationToken: CancellationToken.None);

        await Assert.That(said).IsNotEmpty()
            .Because("a connect that says nothing is a connect a person cannot tell from a "
                   + "hang, which is how both ends of this path came to blame each other.");

        var whole = string.Join(" | ", said);

        await Assert.That(whole).Contains("key")
            .Because("nothing is sealed until the key is checked, and a refusal at that step "
                   + "means something quite different from one later. Said: " + whole);
        await Assert.That(whole).Contains("route")
            .Because("finding a route is this machine's own half, and the only step whose "
                   + "failure is about the network between them. Said: " + whole);
        await Assert.That(whole).Contains("heartbeat")
            .Because("this is the long wait and the reason it is long: an offer is picked up "
                   + "on the runner's next beat, so a person needs to know they are waiting "
                   + "for a machine to come round rather than for a network. Said: " + whole);
    }

    [Test]
    public async Task Saying_nothing_is_allowed_and_changes_no_outcome()
    {
        // THE OLD CALLERS. Narration is a second audience for a method that
        // already had one; a caller that wants none must not have to invent a
        // sink, nor get a different answer for passing none.
        using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var reached = await new ConsoleChannel([], TimeSpan.FromSeconds(2)).ReachAsync(
            An(Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo())),
            ephemeral,
            FreshPins(),
            T0,
            (_, _) => Task.CompletedTask,
            _ => Task.FromResult(Collected.NotYet),
            saying: null,
            cancellationToken: CancellationToken.None);

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.RunnerNeverAnswered);
    }

    [Test]
    public async Task A_key_that_changed_stops_before_it_has_looked_for_a_route()
    {
        // THE ORDER IS THE SECURITY PROPERTY. Sealing first and checking after
        // would have sent the offer - candidates and all - to whoever
        // substituted the key. The narration must not describe work that has to
        // not have happened yet, which makes it a second reader of that order.
        using var pinnedKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var substituted = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var pins = FreshPins();

        // PINNED BY USE, which is how this type records one - trust on first
        // use, as a side effect of the first check.
        pins.Check(TheRunner, Convert.ToBase64String(pinnedKey.ExportSubjectPublicKeyInfo()), T0);

        var said = new List<string>();
        var left = false;

        var reached = await new ConsoleChannel([], TimeSpan.FromSeconds(2)).ReachAsync(
            An(Convert.ToBase64String(substituted.ExportSubjectPublicKeyInfo())),
            ephemeral,
            pins,
            T0,
            (_, _) => { left = true; return Task.CompletedTask; },
            _ => Task.FromResult(Collected.NotYet),
            saying: said.Add,
            cancellationToken: CancellationToken.None);

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.KeyChanged);
        await Assert.That(left).IsFalse()
            .Because("nothing may be sent to a key this console did not pin.");

        await Assert.That(said.Any(s => s.Contains("route", StringComparison.Ordinal))).IsFalse()
            .Because("saying it looked for a route would describe work that must not have "
                   + "happened. Said: " + string.Join(" | ", said));
    }
}
