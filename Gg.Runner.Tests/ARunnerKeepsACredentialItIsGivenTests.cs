using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner writes a credential a person hands it over the channel, and a runner
/// nobody wired to keep one cannot be given one at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arm the vocabulary now requires.</b>
/// <c>ChannelDispatchIsClosedTests.Every_kind_in_the_vocabulary_has_an_arm</c>
/// states the rule from the other side: <i>a value added to RunnerAskKinds with
/// no arm here would be a kind the contract says exists and the runner silently
/// refuses - which reads to a console exactly like a runner one version
/// behind.</i> So the contract value and this arrive together.
/// </para>
/// <para>
/// <b>THE LOCK THAT STAYS, and it is the one the private key already has.</b>
/// <c>Gg.Runner</c> never goes looking for a credential store - the composition
/// root either hands in a way to keep one or does not - so "this runner may be
/// given a credential" is a wiring decision somebody made rather than a
/// capability every runner has. Default closed, and asserted rather than
/// intended.
/// </para>
/// <para>
/// <b>The locator is validated before it becomes a path.</b>
/// <c>CredentialStore</c> already says why, about a locator arriving from a
/// control plane: <i>a path it could steer is a path it could steer
/// anywhere</i>. This one arrives from a console over a channel the ADR calls
/// hostile, which is the same guard and a better reason for it. Refused rather
/// than sanitised: sanitising means deciding what somebody meant by
/// <c>../../etc/passwd</c>.
/// </para>
/// </remarks>
public class ARunnerKeepsACredentialItIsGivenTests
{

    /// <summary>
    /// A sealed credential, for a test that is about the dispatch rather than
    /// about cryptography.
    /// </summary>
    /// <remarks>
    /// Sealed to a throwaway key: the arm under test writes what it is handed
    /// and never opens it, which is slice fifty-nine step 5's whole point.
    /// </remarks>
    private static Gg.Contracts.SealedCredential Sealed(string value = "ghp-not-a-real-token") =>
        Gg.Contracts.CredentialSeal.Seal(
            value,
            [Convert.ToBase64String(
                System.Security.Cryptography.ECDiffieHellman
                    .Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256)
                    .PublicKey.ExportSubjectPublicKeyInfo())]);
    private const string TheSecret = "ghp-not-a-real-token-9f3b2a7c05e8";

    /// <summary>A store that remembers, so a test can look.</summary>
    private sealed class AStore : IKeepACredential
    {
        public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

        public bool Keep(string locator, Gg.Contracts.SealedCredential envelope) =>
            KeepLocallyMinted(locator, envelope.Ciphertext);

        public bool KeepLocallyMinted(string locator, string secret)
        {
            Written[locator] = secret;
            return true;
        }
    }

    /// <summary>Says nothing about itself; this test is about the third arm.</summary>
    private sealed class Quiet : IAnswersAboutItself
    {
        public LogTail Tail(int lines) => new() { Lines = [], Truncated = false };

        public RunnerStatusReport Status() => new()
        {
            Doing = "nothing",
            At = DateTimeOffset.UnixEpoch,
        };
    }

    /// <summary>The envelope the ask under test carries, kept so a test can compare it.</summary>
    /// <remarks>
    /// <b>Built once and remembered, because the assertion is now about the
    /// ENVELOPE arriving unopened.</b> Sealing twice would produce two different
    /// ciphertexts for one value - a fresh content key and nonce each time - and
    /// a test comparing them would fail for a runner doing exactly the right
    /// thing.
    /// </remarks>
    private static SealedCredential TheEnvelope { get; } = Sealed(TheSecret);

    private static RunnerAsk Configuring(string locator, SealedCredential? envelope = null) => new()
    {
        Kind = RunnerAskKinds.ConfigureCredential,
        ConfigureCredential = new ConfigureCredentialAsk
        {
            Locator = locator,
            Envelope = envelope ?? TheEnvelope,
        },
    };

    [Test]
    public async Task A_wired_runner_keeps_what_it_is_given()
    {
        var store = new AStore();
        var dispatch = new AskDispatch(new Quiet(), store);

        var said = dispatch.Answer(Configuring("local:acme/widgets"));

        await Assert.That(said!.Kind).IsEqualTo(RunnerAskKinds.ConfigureCredential);
        await Assert.That(said.Configured!.Locator).IsEqualTo("local:acme/widgets");
        await Assert.That(said.Configured.Written).IsTrue();
        // WHAT IT WAS GIVEN, WHICH IS THE ENVELOPE. A runner writing the
        // credential it was handed never sees the value, so this compares the
        // ciphertext - there is no plaintext on this path to compare against.
        await Assert.That(store.Written["local:acme/widgets"]).IsEqualTo(TheEnvelope.Ciphertext);
    }

    [Test]
    public async Task A_runner_nobody_wired_to_keep_one_cannot_be_given_one()
    {
        // THE COMPOSITION ROOT DECIDES, which is how the private key already
        // works: "Gg.Runner never goes looking for it - it lives on the machine
        // and never leaves it - so the composition root either hands in a way to
        // open a session or does not." A capability every runner has by default
        // is the standing grant this whole path exists to not be.
        var dispatch = new AskDispatch(new Quiet());

        var said = dispatch.Answer(Configuring("local:acme/widgets"));

        // AND IT SAYS SO, which it did not. Returning null made this
        // indistinguishable from a runner too old to have the arm at all - and
        // the sender prints "either it is running a gg that predates this, or
        // the ask did not reach it" for silence, which sends somebody to check
        // versions when the answer is one line in a file on this machine.
        //
        // THE SENTENCE FOR THIS CASE ALREADY EXISTED AND WAS UNREACHABLE.
        // SendACredential's written-false arm names accept-configured and says
        // whose decision it is; nothing could ever trigger it.
        await Assert.That(said).IsNotNull();
        await Assert.That(said!.Kind).IsEqualTo(RunnerAskKinds.ConfigureCredential);
        await Assert.That(said.Configured!.Written).IsFalse()
            .Because("it heard and did not keep it, which is a different fact from never "
                   + "having heard - and only one of the two is fixed on this machine.");
        await Assert.That(said.Configured.Locator).IsEqualTo("local:acme/widgets")
            .Because("the sender names the locator in what it prints, and it is the locator "
                   + "the sender itself sent - nothing is disclosed by echoing it.");

        await Assert.That(dispatch.Refused).IsEqualTo(1)
            .Because("counted rather than logged, like every other refusal here - a hostile "
                   + "peer must not be able to make a runner write to its own disk. Answering "
                   + "does not make it less of a refusal.");
    }

    [Test]
    public async Task A_locator_that_could_steer_a_path_is_refused_rather_than_written()
    {
        // REFUSED RATHER THAN SANITISED, which is CredentialStore's own ruling:
        // "sanitising means deciding what somebody meant by '../../etc/passwd',
        // and there is no answer to that question that is better than saying
        // no." Held HERE as well as there, because this is the machine whose
        // disk it would be, and a bound only the far end enforces disappears the
        // moment the far end is wrong.
        var store = new AStore();
        var dispatch = new AskDispatch(new Quiet(), store);

        foreach (var steered in (string[])
            ["../../etc/passwd", "local:../../etc/passwd", "/etc/passwd", "local:"])
        {
            var said = dispatch.Answer(Configuring(steered));

            await Assert.That(said).IsNull()
                .Because($"'{steered}' is not a locator, and a locator is what becomes a path. "
                       + "SILENT, UNLIKE THE ARM ABOVE, and the asymmetry is deliberate: a "
                       + "machine that is not opted in is answering somebody who typed a real "
                       + "command, and a steered locator is not something the sender can "
                       + "produce - so one is a person to help and the other is not.");
        }

        await Assert.That(store.Written).IsEmpty()
            .Because("nothing was written, which is the half a refusal that answered "
                   + "politely would still have got wrong.");
    }

    [Test]
    public async Task The_secret_appears_in_nothing_the_runner_says_back()
    {
        // THE RUNNER RULE, ASSERTED RATHER THAN INTENDED: "the resolved secret
        // never leaves this machine. It must not appear in any outbound request
        // body, log line, trace or span." A runner that echoed what it had been
        // handed would put it back on a channel a person is watching and into
        // whatever renders it - which is the one failure this path cannot have.
        var dispatch = new AskDispatch(new Quiet(), new AStore());

        var said = dispatch.Answer(Configuring("local:acme/widgets"));

        await Assert.That(System.Text.Json.JsonSerializer.Serialize(
                said, ChannelJson.Default.RunnerSaid))
            .DoesNotContain(TheSecret);

        // Poison twin: the serializer saw something, so its silence is silence
        // about the secret rather than about everything.
        await Assert.That(System.Text.Json.JsonSerializer.Serialize(
                said, ChannelJson.Default.RunnerSaid))
            .Contains("local:acme/widgets");
    }
}
