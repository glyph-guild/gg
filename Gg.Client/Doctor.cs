namespace Gg.Client;

/// <summary>The checks <c>gg doctor</c> runs.</summary>
/// <remarks>
/// Only what exists. Credential resolution joins at step 5; a check that
/// passed because the feature is absent would be the same lie as a stub verb.
/// </remarks>
public static class DoctorChecks
{
    public const string ControlPlane = "control plane";
    public const string Protocol = "protocol";
    public const string Session = "session";
    public const string Runner = "runner";

    /// <summary>Where the control plane sends telemetry, if anywhere.</summary>
    /// <remarks>
    /// Reported, never judged. Whether a destination is acceptable is the
    /// customer's decision about their own deployment; gg's job is to make the
    /// fact askable, because ambient environment once chose one that nothing in
    /// either repository had configured.
    /// </remarks>
    public const string Telemetry = "telemetry";
}

/// <summary>
/// One thing gg looked at.
/// </summary>
/// <remarks>
/// <b>Blocking and fixable are answered separately</b>, and that pairing is
/// the whole design. Collapsing them into one severity loses the two cases
/// that matter most: a blocking problem the person cannot fix themselves -
/// which is a support call and should say so - and a non-blocking one they
/// can, which is the entire value of a doctor command.
/// </remarks>
public sealed record DoctorCheck
{
    public required string Name { get; init; }

    public required bool Passed { get; init; }

    /// <summary>What was found, in a sentence somebody can act on.</summary>
    public required string Detail { get; init; }

    /// <summary>Whether this stops gg from working at all.</summary>
    public required bool Blocking { get; init; }

    /// <summary>Whether the person at this machine can do something about it.</summary>
    public required bool Fixable { get; init; }

    /// <summary>What to do, when there is something. Never set without <see cref="Fixable"/>.</summary>
    public string? Fix { get; init; }
}

/// <summary>Everything gg looked at, and what it makes of it.</summary>
public sealed record DoctorReport
{
    public required IReadOnlyList<DoctorCheck> Checks { get; init; }

    /// <summary>
    /// Non-zero only when something BLOCKING failed.
    /// </summary>
    /// <remarks>
    /// A doctor that always exits zero is decoration in a script, and one that
    /// exits non-zero on a warning is one people stop running.
    /// </remarks>
    public int ExitCode => Checks.Any(c => !c.Passed && c.Blocking) ? 1 : 0;
}

/// <summary>
/// Answers "why is this not working" without needing a flight.
/// </summary>
/// <remarks>
/// The checks run in dependency order and later ones are honest about being
/// unable to run: a session cannot be validated against a control plane that
/// cannot be reached, and reporting the session as broken in that case would
/// send somebody to re-authenticate for no reason.
/// </remarks>
public sealed class Doctor(ControlPlaneClient client, ISessionStore sessions, Uri controlPlane)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;
    private readonly Uri _controlPlane = controlPlane;

    public async Task<DoctorReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<DoctorCheck>();

        var stored = _sessions.Read();
        var reachable = true;
        var protocolRefusal = (string?)null;

        // Connectivity and the protocol floor come from the same request: the
        // floor is checked before authentication server-side, so an anonymous
        // call reaches it, and a 426 answers both questions at once.
        try
        {
            await _client.PingAsync(cancellationToken);
        }
        catch (ProtocolTooOldException refusal)
        {
            protocolRefusal = refusal.Message;
        }
        catch (HttpRequestException failure)
        {
            reachable = false;
            checks.Add(new DoctorCheck
            {
                Name = DoctorChecks.ControlPlane,
                Passed = false,
                Detail = $"could not connect to {_controlPlane}: {failure.Message}",
                Blocking = true,
                // Nothing on this machine changes whether a remote service is
                // up. Telling somebody to try is how a support call starts
                // badly.
                Fixable = false,
            });
        }

        if (reachable)
        {
            checks.Add(new DoctorCheck
            {
                Name = DoctorChecks.ControlPlane,
                Passed = true,
                Detail = $"reachable at {_controlPlane}",
                Blocking = true,
                Fixable = false,
            });
        }

        checks.Add(protocolRefusal is { } refusalDetail
            ? new DoctorCheck
            {
                Name = DoctorChecks.Protocol,
                Passed = false,
                Detail = refusalDetail,
                Blocking = true,
                // Upgrading is something the person can do, which is exactly
                // why the refusal has to reach them as a diagnosis naming the
                // range rather than as a bare 426.
                Fixable = true,
                Fix = "install a newer gg",
            }
            : new DoctorCheck
            {
                Name = DoctorChecks.Protocol,
                Passed = reachable,
                Detail = reachable
                    ? $"this gg speaks {GgVersions.Protocol}, and the control plane accepts it"
                    : "not checked: the control plane could not be reached",
                Blocking = true,
                // A check that did not run has no fix to offer. Suggesting an
                // upgrade here would send somebody to reinstall gg over a
                // network problem - advice that is worse than silence, because
                // following it costs them time and changes nothing.
                Fixable = reachable,
                Fix = reachable ? "install a newer gg" : null,
            });

        checks.Add(await SessionCheckAsync(stored, reachable, protocolRefusal is null, cancellationToken));
        checks.Add(await TelemetryCheckAsync(stored, reachable, protocolRefusal is null, cancellationToken));
        checks.Add(RunnerCheck(stored));

        return new DoctorReport { Checks = checks };
    }

    private async Task<DoctorCheck> SessionCheckAsync(
        StoredSession? stored, bool reachable, bool protocolOk, CancellationToken cancellationToken)
    {
        if (stored is null)
        {
            return new DoctorCheck
            {
                Name = DoctorChecks.Session,
                Passed = false,
                Detail = "not signed in",
                Blocking = true,
                Fixable = true,
                Fix = "gg login",
            };
        }

        if (!reachable || !protocolOk)
        {
            // Honest rather than convenient: a session cannot be validated
            // against a control plane we could not reach, and reporting it
            // broken would send somebody to re-authenticate for no reason.
            return new DoctorCheck
            {
                Name = DoctorChecks.Session,
                Passed = false,
                Detail = "not checked: the control plane could not be asked",
                Blocking = true,
                // As above: signing in again would not help, because nothing
                // established that the session is the problem.
                Fixable = false,
            };
        }

        // Asked of the SERVER, not of the stored expiry. Held locally and dead
        // server-side is precisely the case a local check would call healthy.
        var who = await _client.WhoAmIAsync(stored.SessionToken, cancellationToken);

        return who is null
            ? new DoctorCheck
            {
                Name = DoctorChecks.Session,
                Passed = false,
                Detail = "the control plane no longer honours this session",
                Blocking = true,
                Fixable = true,
                Fix = "gg login",
            }
            : new DoctorCheck
            {
                Name = DoctorChecks.Session,
                Passed = true,
                Detail = $"{who.PrincipalDisplay}, valid until {who.ExpiresAt:u}",
                Blocking = true,
                Fixable = true,
                Fix = "gg login",
            };
    }

    /// <summary>
    /// What the control plane says it transmits, and where.
    /// </summary>
    /// <remarks>
    /// Never blocking and never failing on the destination itself. Whether a
    /// collector is acceptable is the customer's decision about their own
    /// deployment - gg reports the fact so the decision can be made at all.
    /// </remarks>
    private async Task<DoctorCheck> TelemetryCheckAsync(
        StoredSession? stored, bool reachable, bool protocolOk, CancellationToken cancellationToken)
    {
        if (stored is null || !reachable || !protocolOk)
        {
            return new DoctorCheck
            {
                Name = DoctorChecks.Telemetry,
                Passed = false,
                Detail = "not checked: the control plane could not be asked",
                Blocking = false,
                Fixable = false,
            };
        }

        var disclosure = await _client.TelemetryAsync(stored.SessionToken, cancellationToken);

        return new DoctorCheck
        {
            Name = DoctorChecks.Telemetry,
            // Reporting a destination is not a failure. A control plane that
            // exports somewhere the customer chose is working correctly, and a
            // check that went red on it would train them to ignore this line.
            Passed = true,
            Detail = disclosure is null
                ? "this control plane is too old to say"
                : disclosure.Exporting
                    ? $"the control plane exports to {disclosure.Destination}"
                    : "the control plane exports nothing",
            Blocking = false,
            Fixable = false,
        };
    }

    /// <summary>
    /// Whether this machine has a runner registered.
    /// </summary>
    /// <remarks>
    /// NOT blocking. A person can list their flights, open one and read a log
    /// with no runner at all; calling that blocking would train them to ignore
    /// the word, and then to ignore it on the check that matters.
    /// </remarks>
    private static DoctorCheck RunnerCheck(StoredSession? stored) =>
        new()
        {
            Name = DoctorChecks.Runner,
            Passed = stored is not null,
            Detail = stored is not null
                ? "a session is held, so gg runner up can register one"
                : "no session, so no runner can be registered from here",
            Blocking = false,
            Fixable = true,
            Fix = "gg runner up",
        };
}
