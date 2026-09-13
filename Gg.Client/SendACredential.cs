using System.Security.Cryptography;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>What became of trying to put a credential on a runner.</summary>
/// <remarks>
/// <b>Every one of these sends somebody somewhere different</b>, which is why
/// there is a kind rather than a string — <see cref="WatchOutcome"/>'s rule, and
/// this path has one more place to fail than watching does: the runner can
/// answer and still not have written anything.
/// </remarks>
public enum SendOutcome
{
    /// <summary>It is on the machine.</summary>
    Sent,

    /// <summary>No runner by that id, in this tenant.</summary>
    NoSuchRunner,

    /// <summary>It is not beating, so there is nothing to reach.</summary>
    Offline,

    /// <summary>The control plane would not introduce this person to it.</summary>
    NotIntroduced,

    /// <summary>Nobody answered, or the key did not match.</summary>
    NotReached,

    /// <summary>There was no secret to send.</summary>
    NothingToSend,

    /// <summary>
    /// The runner heard and did not write it.
    /// </summary>
    /// <remarks>
    /// <b>Its own outcome, and the one this path adds.</b> A runner that has not
    /// been told it may be configured refuses for want of a port, and a runner
    /// whose disk is full says so — both arrive here as an answer rather than as
    /// silence, and collapsing them into <see cref="NotReached"/> would send
    /// somebody to check a network that is fine.
    /// </remarks>
    NotWritten,
}

/// <summary>What happened, and the sentence to show for it.</summary>
public sealed record Sent(SendOutcome Outcome, string Said);

/// <summary>
/// Putting a credential on one runner: introduce, reach, hand it over.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="WatchARunner"/>'s shape, with a different verb at the end.</b>
/// The fleet is read first for its reason — a runner that is not beating cannot
/// be reached however correct everything else is, and reaching anyway spends the
/// whole patience before saying so, which reads as a broken machine.
/// </para>
/// <para>
/// <b>The secret crosses sealed, end to end, and the control plane brokers the
/// introduction without being able to read it.</b> Article VIII is not bent by
/// that: the control plane stores references and facts, and a value it never
/// sees is the aligned answer to "how does a machine with no filesystem anybody
/// can reach get a token", rather than a transgression against the article.
/// </para>
/// <para>
/// <b>Nothing here logs the secret and nothing returns it.</b> The one sentence
/// a person gets names the locator and where the value came from, because
/// "which credential am I about to send" is the fact they check before sending
/// it — and naming that is what makes the silence about the value meaningful
/// rather than total.
/// </para>
/// </remarks>
public sealed class SendACredential(ControlPlaneClient control, ConsoleChannel channel)
{
    /// <summary>What this reach asks a capability to authorise.</summary>
    /// <remarks>
    /// <b>Not <see cref="WatchARunner.Purpose"/>, and that difference is the
    /// whole reason a purpose exists.</b> Until a console could say which it
    /// wanted, every introduction was minted for tailing a log and the
    /// narrowing was decoration.
    /// </remarks>
    public static string Purpose => RunnerCapabilityPurposes.ConfigureThisRunner;

    /// <summary>
    /// The ask this puts on the channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Named so the two halves can be checked against each other.</b>
    /// <c>AskDispatch</c> matches the kind AND the member beside it, so a kind
    /// sent bare is refused and counted — which reaches a console as silence,
    /// and reaches a person as a runner that did not answer. It has happened:
    /// a follow loop sent a status ask with no payload and the whole feature
    /// did nothing on a real machine while every test agreed it worked.
    /// </para>
    /// <para>
    /// <b>Built here rather than inline for that reason alone.</b> Inside
    /// <c>SendAsync</c> it cannot be reached without a live channel, so nothing
    /// could ask the one question that matters: is what this side sends
    /// something the other side answers.
    /// </para>
    /// </remarks>
    public static RunnerAsk Asking(string locator, string secret) => new()
    {
        Kind = RunnerAskKinds.ConfigureCredential,
        ConfigureCredential = new ConfigureCredentialAsk
        {
            Locator = locator,
            Secret = secret,
        },
    };

    /// <summary>
    /// The secret to send: this machine's copy, or one typed now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stored one first, because the ordinary case is somebody who has
    /// already run <c>gg credential add</c>.</b> The reference is registered and
    /// the secret is in a 0600 file; asking them to paste it a second time sends
    /// them to go and find it again, and the likeliest place they find it is
    /// where they were told not to keep it.
    /// </para>
    /// <para>
    /// <b>Prompted when this machine has none, which is a real case rather than
    /// a fallback.</b> A credential only a pool member needs was never added
    /// here, and refusing would make somebody store a secret on a laptop purely
    /// to move it to a container.
    /// </para>
    /// <para>
    /// <b>Null for an empty answer</b>, which is
    /// <c>NoCredentialResolver</c>'s disposition at a distance: <i>an empty
    /// secret is a secret that fetches nothing and fails much later, in a place
    /// with no way back to here.</i> Writing one to a runner is exactly that,
    /// on somebody else's machine.
    /// </para>
    /// </remarks>
    public static string? SecretFor(
        ICredentialStore store, string locator, ISecretPrompt prompt, Action<string>? saying)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(prompt);

        var say = saying ?? (_ => { });

        // A LOCATOR THE STORE REFUSES IS NOT A CRASH. By the time one reaches
        // here it was derived from what somebody typed, and a malformed one has
        // to produce a sentence rather than a stack trace.
        string? held;
        try
        {
            held = store.Read(locator);
        }
        catch (ArgumentException)
        {
            held = null;
        }

        if (held is { Length: > 0 })
        {
            // THE LOCATOR, NEVER THE VALUE. Which credential is about to be sent
            // is what a person checks before sending it.
            say($"sending the credential this machine holds for {locator}");
            return held;
        }

        say($"this machine holds no credential for {locator}");

        var typed = prompt.ReadSecret($"Secret for {locator} (not echoed): ");

        return typed is { Length: > 0 } ? typed : null;
    }

    /// <summary>Introduces, reaches, and hands the credential over.</summary>
    public async Task<Sent> SendAsync(
        string sessionToken,
        string runnerId,
        string locator,
        string secret,
        PinnedRunnerKeys pins,
        DateTimeOffset now,
        Action<string>? saying = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pins);

        var say = saying ?? (_ => { });

        say("asking the control plane which runners you can see");

        var fleet = await control.ListRunnersAsync(sessionToken, cancellationToken);

        if (fleet.Runners.FirstOrDefault(r =>
                string.Equals(r.RunnerId, runnerId, StringComparison.OrdinalIgnoreCase))
            is not { } runner)
        {
            return new Sent(
                SendOutcome.NoSuchRunner,
                $"There is no runner {runnerId} here. `gg runners` lists the ones you can see.");
        }

        // ASKED BEFORE ANYTHING IS MINTED, for WatchARunner's reason: this would
        // otherwise be twenty seconds of silence and then a sentence about a
        // machine that is simply switched off.
        if (string.Equals(runner.State, "offline", StringComparison.Ordinal))
        {
            return new Sent(
                SendOutcome.Offline,
                $"{runner.Label} is not beating, so there is nothing to reach. Start it and "
              + "send this again — a credential cannot be left somewhere for a machine to "
              + "collect later, because nothing stores it in between.");
        }

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        say($"asking the control plane to introduce you to {runner.Label}");

        var introduced = await control.IntroduceRunnerAsync(
            sessionToken,
            runner.RunnerId,
            Convert.ToBase64String(ephemeral.ExportSubjectPublicKeyInfo()),
            Purpose,
            cancellationToken);

        if (introduced.Introduction is not { } introduction)
        {
            return new Sent(SendOutcome.NotIntroduced, introduced.Said);
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
            return new Sent(SendOutcome.NotReached, reached.Said);
        }

        using (conversation)
        {
            say($"handing the credential to {runner.Label}");

            var said = await conversation.AskAsync(
                Asking(locator, secret), TimeSpan.FromSeconds(20), cancellationToken);

            // NO ANSWER IS NOT A REFUSAL. A runner one version behind has no arm
            // for this kind and drops the message without a word - which is the
            // dispatch working as designed - and a person told "it refused"
            // would go looking for a permission rather than for a version.
            if (said?.Configured is not { } configured)
            {
                return new Sent(
                    SendOutcome.NotWritten,
                    $"{runner.Label} did not answer about the credential. Either it is "
                  + "running a gg that predates this, or the ask did not reach it. Nothing "
                  + "was written, and nothing was left behind.");
            }

            // WRITTEN: FALSE IS AN ANSWER, and the likeliest one. A runner whose
            // own file does not say accept-configured is handed nowhere to keep
            // a credential and says so, which is a decision somebody made on
            // that machine rather than a fault.
            return configured.Written
                ? new Sent(
                    SendOutcome.Sent,
                    $"{runner.Label} now holds the credential for {configured.Locator}.")
                : new Sent(
                    SendOutcome.NotWritten,
                    $"{runner.Label} heard and did not keep it. The likeliest reason is that "
                  + "its own configuration does not say `accept-configured`, which is a "
                  + "decision made on that machine — a laptop opts in by editing its file, "
                  + "and a pool member is opted in by the tenant that created it.");
        }
    }
}
