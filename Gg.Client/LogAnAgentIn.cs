using System.Security.Cryptography;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>How logging a runner's agent in ended.</summary>
public enum LoginOutcome
{
    LoggedIn,
    NoSuchRunner,
    Offline,
    NotIntroduced,
    NotReached,
    NotStarted,
    NoCode,
    NotWritten,
}

public sealed record LoggedIn(LoginOutcome Outcome, string Said);

/// <summary>
/// Logs a runner's agent in from here: the runner runs its agent's own
/// ceremony, this console shows the person the URL and carries back the code.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same reach as a send, and the same purpose.</b> An introduction
/// minted to configure this runner, because what the ceremony does is place a
/// credential on it: the runner mints the value instead of being handed one,
/// and that is a difference in where the secret comes from, not in what the
/// machine ends up holding.
/// </para>
/// <para>
/// <b>Two asks over one conversation, with a person between them.</b> The
/// conversation stays open while they visit the browser; the runner's own
/// bound on a quiet conversation is the same ten minutes its ceremony waits.
/// </para>
/// <para>
/// <b>An answer of another kind is not this ask's answer.</b> There is no
/// correlation id on the channel; <see cref="Answers"/> is what keeps a late
/// <c>begin</c> answer out of the <c>finish</c> wait.
/// </para>
/// </remarks>
public sealed class LogAnAgentIn(ControlPlaneClient control, ConsoleChannel channel)
{
    public static string Purpose => RunnerCapabilityPurposes.ConfigureThisRunner;

    /// <summary>Longer than the runner's own bound on the URL, so its refusal arrives before this gives up.</summary>
    public static readonly TimeSpan BeginPatience = TimeSpan.FromSeconds(45);

    /// <summary>Longer than the runner's own bound on the token, for the same reason.</summary>
    public static readonly TimeSpan FinishPatience = TimeSpan.FromSeconds(120);

    /// <summary>What the person is asked, with the echo off.</summary>
    public const string CodePrompt = "Code from the browser (not echoed): ";

    public static RunnerAsk Beginning(string provider) => new()
    {
        Kind = RunnerAskKinds.BeginAgentLogin,
        BeginAgentLogin = new BeginAgentLoginAsk { Provider = provider },
    };

    public static RunnerAsk Finishing(string provider, string code) => new()
    {
        Kind = RunnerAskKinds.FinishAgentLogin,
        FinishAgentLogin = new FinishAgentLoginAsk { Provider = provider, Code = code },
    };

    /// <summary>Whether what came back is an answer of the kind that was asked.</summary>
    public static bool Answers(RunnerSaid? said, string kind) =>
        said is not null
        && string.Equals(said.Kind, kind, StringComparison.Ordinal)
        && kind switch
        {
            RunnerAskKinds.BeginAgentLogin => said.LoginBegun is not null,
            RunnerAskKinds.FinishAgentLogin => said.LoginFinished is not null,
            _ => false,
        };

    public static string SaidWhenNothingCameBack(string label) =>
        $"{label} did not answer about the login. Nothing was started, and nothing was written. "
      + "Two things look like this: its own configuration does not say `accept-agent-login` and "
      + "it is old enough to stay quiet about that, or it is running a gg that predates this ask "
      + "entirely. Check `gg config show` and `gg --version` on that machine, in that order. A "
      + "pool member is closed to this by decision: mint a token elsewhere and put it there with "
      + "`gg credential send --runner <id> --agent <name>`.";

    /// <summary>Introduces, reaches, begins, shows the URL, reads the code, finishes.</summary>
    public async Task<LoggedIn> LoginAsync(
        string sessionToken,
        string runnerId,
        string provider,
        PinnedRunnerKeys pins,
        DateTimeOffset now,
        ISecretPrompt prompt,
        Action<string>? openBrowser = null,
        Action<string>? saying = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(prompt);

        var say = saying ?? (_ => { });

        say("asking the control plane which runners you can see");

        var fleet = await control.ListRunnersAsync(sessionToken, cancellationToken);

        if (fleet.Runners.FirstOrDefault(r =>
                string.Equals(r.RunnerId, runnerId, StringComparison.OrdinalIgnoreCase))
            is not { } runner)
        {
            return new LoggedIn(
                LoginOutcome.NoSuchRunner,
                $"There is no runner {runnerId} here. `gg runners` lists the ones you can see.");
        }

        if (string.Equals(runner.State, "offline", StringComparison.Ordinal))
        {
            return new LoggedIn(
                LoginOutcome.Offline,
                $"{runner.Label} is not beating, so there is nothing to reach. Start it and try "
              + "again - the ceremony runs on that machine, and a machine that is off cannot run "
              + "it.");
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
            return new LoggedIn(LoginOutcome.NotIntroduced, introduced.Said);
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
            return new LoggedIn(LoginOutcome.NotReached, reached.Said);
        }

        using (conversation)
        {
            say($"asking {runner.Label} to begin its {provider} login");

            var begun = await conversation.AskAsync(Beginning(provider), BeginPatience, cancellationToken);

            if (!Answers(begun, RunnerAskKinds.BeginAgentLogin))
            {
                return new LoggedIn(LoginOutcome.NotStarted, SaidWhenNothingCameBack(runner.Label));
            }

            var started = begun!.LoginBegun!;
            if (!started.Started || started.Url is not { Length: > 0 } url)
            {
                return new LoggedIn(
                    LoginOutcome.NotStarted,
                    $"{runner.Label} did not begin the login: {started.Diagnosis ?? "no reason was given."}");
            }

            // THE URL, SAID AND OPENED. Said first, because the browser is
            // best-effort and the sentence is what a person copies when it
            // does not open - or when this console is a shell on a machine
            // with no display.
            say($"open this in a browser and sign in: {url}");
            if (started.ExpiresAt is { } expiresAt)
            {
                say($"{runner.Label} waits for the code until {expiresAt:u}");
            }

            openBrowser?.Invoke(url);

            var code = prompt.ReadSecret(CodePrompt);
            if (code is not { Length: > 0 })
            {
                return new LoggedIn(
                    LoginOutcome.NoCode,
                    $"nothing was typed, so nothing was sent. {runner.Label} keeps waiting for a "
                  + "code until the ceremony expires; run this again to be asked for it.");
            }

            say($"handing the code to {runner.Label}");

            var finished = await conversation.AskAsync(Finishing(provider, code), FinishPatience, cancellationToken);

            if (!Answers(finished, RunnerAskKinds.FinishAgentLogin))
            {
                return new LoggedIn(LoginOutcome.NotWritten, SaidWhenNothingCameBack(runner.Label));
            }

            var ended = finished!.LoginFinished!;
            return ended.Written
                ? new LoggedIn(
                    LoginOutcome.LoggedIn,
                    $"{runner.Label} now holds the {provider} token under {ended.Locator}. It looks "
                  + "again on its next check and flies once the agent answers.")
                : new LoggedIn(
                    LoginOutcome.NotWritten,
                    $"{runner.Label} heard the code and kept nothing: {ended.Diagnosis ?? "no reason was given."}");
        }
    }
}
