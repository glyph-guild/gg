

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
    /// Starts a device authorization, shows the human what to do, and polls
    /// until it resolves.
    /// </summary>
    public async Task<int> LoginAsync(string deviceLabel, CancellationToken cancellationToken = default)
    {
        var started = await _client.StartDeviceAuthorizationAsync(deviceLabel, cancellationToken);

        _output.WriteLine();
        _output.WriteLine($"  Open:  {started.VerificationUri}");
        _output.WriteLine($"  Code:  {started.UserCode}");
        _output.WriteLine();
        _output.WriteLine("Waiting for you to approve...");

        // The server sets the cadence. Polling faster than it asked is how a
        // client earns a rate limit for everyone.
        var interval = TimeSpan.FromSeconds(started.PollIntervalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_clock.UtcNow >= started.ExpiresAt)
            {
                _output.WriteLine("That code expired before it was approved. Run gg login again.");
                return 1;
            }

            await _delay(interval, cancellationToken);

            switch (await _client.PollDeviceAuthorizationAsync(started.DeviceCode, cancellationToken))
            {
                case DevicePollResult.Pending:
                    continue;

                case DevicePollResult.Declined declined:
                    _output.WriteLine(declined.Reason);
                    return 1;

                case DevicePollResult.Complete complete:
                    _sessions.Write(new StoredSession
                    {
                        SessionToken = complete.Session.SessionToken,
                        ExpiresAt = complete.Session.ExpiresAt,
                        TenantId = complete.Session.TenantId,
                        PrincipalDisplay = complete.Session.PrincipalDisplay,
                    });
                    _output.WriteLine($"Signed in as {complete.Session.PrincipalDisplay}.");
                    return 0;
            }
        }

        return 1;
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

    /// <summary>Reports the principal, its tenant, and when the session expires.</summary>
    public async Task<int> WhoAmIAsync(CancellationToken cancellationToken = default)
    {
        var stored = _sessions.Read();
        if (stored is null)
        {
            _output.WriteLine("Not signed in. Run gg login.");
            return 1;
        }

        var who = await _client.WhoAmIAsync(stored.SessionToken, cancellationToken);
        if (who is null)
        {
            _output.WriteLine("This session is no longer valid. Run gg login.");
            return 1;
        }

        _output.WriteLine($"  Principal:  {who.PrincipalDisplay} ({who.PrincipalId})");
        _output.WriteLine($"  Tenant:     {who.TenantId}");
        _output.WriteLine($"  Expires:    {who.ExpiresAt:u}");
        return 0;
    }
}
