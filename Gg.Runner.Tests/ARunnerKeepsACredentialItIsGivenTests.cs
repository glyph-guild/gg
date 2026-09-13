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
    private const string TheSecret = "ghp-not-a-real-token-9f3b2a7c05e8";

    /// <summary>A store that remembers, so a test can look.</summary>
    private sealed class AStore : IKeepACredential
    {
        public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

        public bool Keep(string locator, string secret)
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

    private static RunnerAsk Configuring(string locator, string secret = TheSecret) => new()
    {
        Kind = RunnerAskKinds.ConfigureCredential,
        ConfigureCredential = new ConfigureCredentialAsk { Locator = locator, Secret = secret },
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
        await Assert.That(store.Written["local:acme/widgets"]).IsEqualTo(TheSecret);
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

        await Assert.That(said).IsNull()
            .Because("a runner with nowhere to keep a credential must refuse rather than "
                   + "answer that it kept one.");
        await Assert.That(dispatch.Refused).IsEqualTo(1)
            .Because("counted rather than logged, like every other refusal here - a hostile "
                   + "peer must not be able to make a runner write to its own disk.");
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
                .Because($"'{steered}' is not a locator, and a locator is what becomes a path.");
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
