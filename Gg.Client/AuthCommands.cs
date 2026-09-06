

namespace Gg.Client;

/// <summary>Where command output goes. Injected so tests read it.</summary>
public interface IConsoleWriter
{
    void WriteLine(string line = "");
}

/// <summary>Writes to standard output.</summary>
public sealed class StandardConsoleWriter : IConsoleWriter
{
    public void WriteLine(string line = "") => System.Console.WriteLine(line);
}

/// <summary>
/// What waiting on a device authorization came to.
/// </summary>
/// <remarks>
/// <b>A sentence, never an exception.</b> Declined, expired and approved are
/// all ordinary answers to "has somebody said yes yet"; a caller made to catch
/// two of them would invent a policy for each, and the policy invented under
/// time pressure is to carry on as though it had worked.
/// </remarks>
public sealed record SignInResult
{
    /// <summary>Whether this machine now holds a session.</summary>
    public bool SignedIn { get; init; }

    /// <summary>What a person reads. What HAPPENED, never what it becomes.</summary>
    public required string Said { get; init; }
}

/// <summary>
/// The three authentication verbs. No terminal UI here - that is step 4.
/// </summary>
public sealed class AuthCommands(
    ControlPlaneClient client,
    ISessionStore sessions,
    IConsoleWriter output,
    IClock clock,
    Func<TimeSpan, CancellationToken, Task> delay)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;
    private readonly IConsoleWriter _output = output;
    private readonly IClock _clock = clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay;

    /// <summary>
    /// Begins a device authorization and hands back what a person must do.
    /// </summary>
    /// <remarks>
    /// <b>It shows nothing, and that is the difference between its two
    /// callers.</b> The verb below prints the code to a terminal it owns; the
    /// console draws it in a modal, and anything written to standard output
    /// from there lands on a screen Terminal.Gui is about to paint over.
    /// </remarks>
    public Task<Gg.Contracts.DeviceAuthorizationStarted> StartAsync(
        string deviceLabel, CancellationToken cancellationToken = default) =>
        _client.StartDeviceAuthorizationAsync(deviceLabel, cancellationToken);

    /// <summary>
    /// The lines a person needs, written by whoever holds the terminal.
    /// </summary>
    /// <remarks>
    /// <b>Static and given its output, because both callers write these and
    /// neither at the same moment.</b> The verb writes them once, before it
    /// waits. The console writes them again as its UI session comes down - the
    /// modal that had been drawing the code is gone by then, and the wait it is
    /// about to start can outlast a person's patience.
    /// </remarks>
    public static void ShowCode(IConsoleWriter output, Gg.Contracts.DeviceAuthorizationStarted started)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(started);

        output.WriteLine();
        output.WriteLine($"  Open:  {started.VerificationUri}");
        output.WriteLine($"  Code:  {started.UserCode}");
        output.WriteLine();
    }

    /// <summary>
    /// Waits for a person to approve what was started, and stores the session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The only polling loop.</b> The cadence, the expiry check and the three
    /// answers are decided here and nowhere else - a second copy would agree
    /// with this one until somebody edited one of them, and what they would
    /// disagree about is the interval the server asked for.
    /// </para>
    /// <para>
    /// Bounded by the authorization's own expiry rather than by a timeout
    /// invented here, so a caller that has given a person the screen gets it
    /// back with a sentence rather than never.
    /// </para>
    /// </remarks>
    public async Task<SignInResult> AwaitApprovalAsync(
        Gg.Contracts.DeviceAuthorizationStarted started,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(started);

        // The server sets the cadence. Polling faster than it asked is how a
        // client earns a rate limit for everyone.
        var interval = TimeSpan.FromSeconds(started.PollIntervalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_clock.UtcNow >= started.ExpiresAt)
            {
                return new SignInResult { Said = "That code expired before it was approved." };
            }

            await _delay(interval, cancellationToken);

            switch (await _client.PollDeviceAuthorizationAsync(started.DeviceCode, cancellationToken))
            {
                case DevicePollResult.Pending:
                    continue;

                case DevicePollResult.Declined declined:
                    return new SignInResult { Said = declined.Reason };

                case DevicePollResult.Complete complete:
                    _sessions.Write(new StoredSession
                    {
                        SessionToken = complete.Session.SessionToken,
                        ExpiresAt = complete.Session.ExpiresAt,
                        TenantId = complete.Session.TenantId,
                        PrincipalDisplay = complete.Session.PrincipalDisplay,
                    });

                    return new SignInResult
                    {
                        SignedIn = true,
                        Said = $"Signed in as {complete.Session.PrincipalDisplay}.",
                    };
            }
        }

        return new SignInResult { Said = "Signing in was cancelled." };
    }

    /// <summary>
    /// Starts a device authorization, shows the human what to do, and polls
    /// until it resolves.
    /// </summary>
    /// <remarks>
    /// The two halves above, and the printing between them. The remedy is
    /// appended HERE rather than carried in the sentence, because this is the
    /// caller that has a shell to run it in - the console is drawn over the one
    /// a person would type it into.
    /// </remarks>
    public async Task<int> LoginAsync(string deviceLabel, CancellationToken cancellationToken = default)
    {
        var started = await StartAsync(deviceLabel, cancellationToken);

        ShowCode(_output, started);
        _output.WriteLine("Waiting for you to approve...");

        var result = await AwaitApprovalAsync(started, cancellationToken);

        _output.WriteLine(result.SignedIn ? result.Said : $"{result.Said} Run gg login again.");

        return result.SignedIn ? 0 : 1;
    }

    /// <summary>
    /// Revokes server-side FIRST, then deletes locally.
    /// </summary>
    /// <remarks>
    /// The order is the point. Deleting locally while the server session stays
    /// live tells the developer they are logged out when they are not - the
    /// credential still works for anyone holding it.
    /// </remarks>
    public async Task<int> LogoutAsync(CancellationToken cancellationToken = default)
    {
        var stored = _sessions.Read();
        if (stored is null)
        {
            _output.WriteLine("Not signed in.");
            return 0;
        }

        var revoked = await _client.RevokeSessionAsync(stored.SessionToken, cancellationToken);
        if (!revoked)
        {
            _output.WriteLine(
                "Could not revoke the session with the control plane, so the local copy has been kept. " +
                "Deleting it here would leave a live session you can no longer revoke. Try again when connected.");
            return 1;
        }

        _sessions.Clear();
        _output.WriteLine("Signed out.");
        return 0;
    }

    /// <summary>
    /// Reports the principal, its tenant, when the session expires, and any
    /// notices.
    /// </summary>
    /// <remarks>
    /// <b>Renders what <see cref="IdentityCommands"/> returns</b>, rather than
    /// composing its own lines from the same response. That is what makes the
    /// console's notices and this command's the same answer: there is one
    /// renderer, and a field added to <c>WhoAmI</c> appears on both surfaces or
    /// neither.
    /// <para>
    /// It also fixed a silence. These three lines never printed
    /// <c>WhoAmI.Notices</c>, so a tenant whose check-run egress was gone was
    /// told so by the control plane on every single call and by nothing else.
    /// </para>
    /// </remarks>
    public async Task<int> WhoAmIAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var identity = await new IdentityCommands(_client, _sessions)
                .ShowAsync(cancellationToken);

            _output.WriteLine(VerbOutput.ToText(identity));
            return 0;
        }
        catch (NotSignedInException refusal)
        {
            _output.WriteLine(refusal.Message);
            return 1;
        }
    }
}
