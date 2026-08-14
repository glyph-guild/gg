using Gg.Contracts;

namespace Gg.Client;

/// <summary>The checks <c>gg doctor</c> runs.</summary>
/// <remarks>
/// Only what exists. Credential resolution joined at step 5 because credential
/// resolution now happens; a check that passed because the feature is absent
/// would be the same lie as a stub verb.
/// </remarks>
public static class DoctorChecks
{
    public const string ControlPlane = "control plane";
    public const string Protocol = "protocol";
    public const string Session = "session";

    /// <summary>Reporting a flight's result back to where it came from.</summary>
    /// <remarks>
    /// The degradation nothing on this machine can detect. Flights keep
    /// running and keep recording facts - the runner uses the customer's own
    /// credential and never needed the control plane's - so the only symptom
    /// is a pull request with no check on it, and nothing about that says why.
    /// </remarks>
    public const string Egress = "egress";
    public const string Runner = "runner";

    /// <summary>
    /// What the envelope's <c>moves</c> actually do.
    /// </summary>
    /// <remarks>
    /// <b>Because the honest answer is weaker than the field name suggests.</b> Moves
    /// are recorded and not enforced: the executor's allow-list does not bind, so a
    /// flight declaring <c>read</c> can edit. Somebody reading an envelope will
    /// otherwise assume a bound exists, and silent degradation writes a line.
    /// </remarks>
    public const string Moves = "moves";

    /// <summary>Where secrets live on this machine, and how they are protected.</summary>
    /// <remarks>
    /// Stated, never judged, and never blocking. A person cannot reason about
    /// a store they cannot find, and this is the only place gg says where it
    /// is - or admits what a mode-0600 file does and does not buy them.
    /// </remarks>
    public const string CredentialStore = "credential store";

    /// <summary>Whether every registered reference resolves on this machine.</summary>
    /// <remarks>
    /// ADR-0004 named this failure before it existed: a runner that cannot
    /// read a secret produces a stalled flight that looks like a broken
    /// product. This is the diagnosis that stops it being one.
    /// </remarks>
    public const string Credentials = "credentials";

    /// <summary>Where the control plane sends telemetry, if anywhere.</summary>
    /// <remarks>
    /// Reported, never judged. Whether a destination is acceptable is the
    /// customer's decision about their own deployment; gg's job is to make the
    /// fact askable, because ambient environment once chose one that nothing in
    /// either repository had configured.
    /// </remarks>
    public const string Telemetry = "telemetry";

    /// <summary>
    /// Whether takeovers are getting the agent's own account, or only
    /// measurements.
    /// </summary>
    /// <remarks>
    /// <b>So the fallback cannot quietly become normal.</b> A seed without the
    /// account still works, which is exactly the danger: handoff degrades to
    /// measurements-only and the feature stops doing the thing it was built for
    /// with nobody noticing. An absent account writes a line here and in the
    /// bundle, because a degradation visible in neither is one somebody reports
    /// and we cannot reproduce.
    /// </remarks>
    public const string HandoffAccount = "handoff account";
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
public enum DoctorOutcome
{
    /// <summary>Nothing was wrong.</summary>
    Pass,

    /// <summary>Something is wrong, and it is about this machine or this setup.</summary>
    Fail,

    /// <summary>
    /// Nothing is wrong; this is how the product works, and it will not change here.
    /// </summary>
    /// <remarks>
    /// <b>Three states, because two booleans only ever described two.</b> A check that is
    /// non-blocking, unfixable and never passing reads as a permanent failure, and a
    /// permanent failure is a line somebody learns to scroll past - which is exactly what
    /// makes the real failures beside it easier to ignore. A disclosure is reported every
    /// time on purpose, so it has to be legible as its own kind of thing rather than as
    /// the failure it sits next to.
    /// </remarks>
    Disclosure,
}

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

    /// <summary>
    /// Whether this is a standing statement about the product rather than a result.
    /// </summary>
    /// <remarks>
    /// Set on the few checks that report something permanently true. Never set together
    /// with <see cref="Passed"/>: a disclosure has not passed, and saying it had would be
    /// the lie in the other direction.
    /// </remarks>
    public bool Discloses { get; init; }

    /// <summary>
    /// Which of the three states this is, which is what anything rendering should read.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored beside the booleans, so the state and the flags cannot
    /// disagree - and a renderer reading this cannot accidentally treat a disclosure as a
    /// failure by looking at <see cref="Passed"/> alone, which is what it did before.
    /// </remarks>
    public DoctorOutcome Outcome =>
        Discloses ? DoctorOutcome.Disclosure
        : Passed ? DoctorOutcome.Pass
        : DoctorOutcome.Fail;
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
public sealed class Doctor(
    ControlPlaneClient client, ISessionStore sessions, ICredentialStore credentials, Uri controlPlane)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;
    private readonly ICredentialStore _credentials = credentials;
    private readonly Uri _controlPlane = controlPlane;

    /// <param name="accountsMissing">
    /// How many recent flights produced no closing account. Passed in because
    /// gg's evidence lives on the other side of an API call and the doctor does
    /// not go looking; the console knows, and telling it is cheaper than a
    /// second fetch.
    /// </param>
    public async Task<DoctorReport> RunAsync(
        int accountsMissing = 0, CancellationToken cancellationToken = default)
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
        checks.Add(MovesCheck());
        checks.Add(HandoffAccountCheck(accountsMissing));
        checks.Add(CredentialStoreCheck());
        checks.Add(await CredentialResolutionCheckAsync(
            stored, reachable, protocolRefusal is null, cancellationToken));
        checks.AddRange(await TenantNoticeChecksAsync(
            stored, reachable, protocolRefusal is null, cancellationToken));

        return new DoctorReport { Checks = checks };
    }

    /// <summary>
    /// What the control plane says is degraded, said here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>None of these can be detected from this machine.</b> That is the
    /// whole reason they travel: the control plane knows its app was
    /// uninstalled, and nothing gg can measure locally does.
    /// </para>
    /// <para>
    /// <b>The sentence is rendered, never composed.</b> gg names no forge, so
    /// a remedy written here would either say nothing useful or would put a
    /// provider's name in this binary. Blocking and fixable come from the
    /// control plane too - a tool that promoted advisories to failures would
    /// make every notice a broken build.
    /// </para>
    /// <para>
    /// Nothing when there is nothing wrong. A doctor that always printed a
    /// green egress line would train somebody to read past the line that
    /// matters.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<DoctorCheck>> TenantNoticeChecksAsync(
        StoredSession? stored, bool reachable, bool protocolOk, CancellationToken cancellationToken)
    {
        // Nothing to ask on behalf of. Reporting somebody else's degradation
        // to an unauthenticated caller would be both wrong and a disclosure.
        if (stored is null || !reachable || !protocolOk)
        {
            return [];
        }

        WhoAmI? who;
        try
        {
            who = await _client.WhoAmIAsync(stored.SessionToken, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // The session check above already reported the connection. A
            // second red line about the same failure is noise.
            return [];
        }

        return
        [
            .. (who?.Notices ?? []).Select(notice => new DoctorCheck
            {
                Name = notice.Code,
                Passed = false,
                // Stripped here: this is the last code between a response body
                // and somebody's terminal, and a notice is externally-sourced
                // text arriving at a renderer like any other.
                Detail = ControlText.Strip(notice.Detail),
                Blocking = notice.Blocking,
                // Answered separately, and never claimed without a remedy to
                // name. "Fixable, but we cannot say how" sends somebody
                // looking for an hour.
                Fixable = notice.Remedy is { Length: > 0 },
                Fix = notice.Remedy is { Length: > 0 } remedy ? ControlText.Strip(remedy) : null,
            }),
        ];
    }

    /// <summary>
    /// Where the secret is, and what that actually protects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It runs in every state, including with no session and no control plane,
    /// because it asks nothing of either. Somebody debugging a credential
    /// problem needs the path before they need anything else.
    /// </para>
    /// <para>
    /// Never blocking and never failing: it is a statement of fact, and a check
    /// that went red on "here is where your secrets live" would train somebody
    /// to skip the line above the one that matters.
    /// </para>
    /// </remarks>
    private DoctorCheck CredentialStoreCheck() =>
        new()
        {
            Name = DoctorChecks.CredentialStore,
            Passed = true,
            Detail = _credentials.Protection,
            Blocking = false,
            Fixable = false,
        };

    /// <summary>
    /// Whether every reference the control plane holds resolves here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blocking, because a flight touching that repository cannot run - and
    /// fixable, because the person at this machine is exactly who can fix it.
    /// The two are answered separately, and the remedy is named: nothing claims
    /// fixable without saying what would fix it.
    /// </para>
    /// <para>
    /// The references come from the control plane and the secrets are looked
    /// for locally, which is the whole shape of the product in one check. It
    /// therefore cannot run without a session, and when it cannot run it offers
    /// no remedy - telling somebody to re-enter a token over a login problem is
    /// advice that costs them time and changes nothing.
    /// </para>
    /// </remarks>
    private async Task<DoctorCheck> CredentialResolutionCheckAsync(
        StoredSession? stored, bool reachable, bool protocolOk, CancellationToken cancellationToken)
    {
        if (stored is null || !reachable || !protocolOk)
        {
            return new DoctorCheck
            {
                Name = DoctorChecks.Credentials,
                Passed = false,
                Detail = "not checked: the control plane could not be asked which credentials are registered",
                Blocking = false,
                Fixable = false,
            };
        }

        var registered = await _client.ListCredentialsAsync(stored.SessionToken, cancellationToken);

        if (registered.Credentials.Count == 0)
        {
            return new DoctorCheck
            {
                Name = DoctorChecks.Credentials,
                Passed = true,
                Detail = "no credentials registered, so there is nothing to resolve",
                Blocking = false,
                Fixable = false,
            };
        }

        // Named individually. "1 of 3 credentials could not be resolved" sends
        // somebody looking; naming the locator ends the search.
        var unresolvable = registered.Credentials
            .Where(c => Missing(c.Reference.Locator))
            .Select(c => $"{c.Reference.Locator} ({c.Repo}, as {c.Reference.Identity})")
            .ToList();

        return unresolvable.Count == 0
            ? new DoctorCheck
            {
                Name = DoctorChecks.Credentials,
                Passed = true,
                Detail = $"all {registered.Credentials.Count} registered credential(s) resolve on this machine",
                Blocking = false,
                Fixable = false,
            }
            : new DoctorCheck
            {
                Name = DoctorChecks.Credentials,
                Passed = false,
                Detail = "registered here but not stored on this machine: " + string.Join(", ", unresolvable),
                // A flight needing one of these cannot run at all, and it fails
                // at the runner where nobody is looking.
                Blocking = true,
                Fixable = true,
                Fix = "gg credential add --repo <slug>, on this machine, for each one listed",
            };
    }

    /// <summary>
    /// Whether a locator has no secret here.
    /// </summary>
    /// <remarks>
    /// A locator the store refuses counts as missing rather than throwing: it
    /// came back from the control plane, and a malformed one is a finding for
    /// this report rather than a crash in the middle of it.
    /// </remarks>
    private bool Missing(string locator)
    {
        try
        {
            return _credentials.Read(locator) is null;
        }
        catch (ArgumentException)
        {
            return true;
        }
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
    /// <summary>
    /// Says when takeovers have been running on measurements alone.
    /// </summary>
    /// <remarks>
    /// A count rather than a boolean: one flight whose runner was killed is
    /// ordinary, and every flight for a week is a broken executor nobody has
    /// noticed. The number is what tells those apart.
    /// </remarks>
    public static DoctorCheck HandoffAccountCheck(int accountsMissing) =>
        new()
        {
            Name = DoctorChecks.HandoffAccount,
            Passed = accountsMissing == 0,
            Detail = accountsMissing == 0
                ? "takeover seeds carry the agent's own account"
                : $"{accountsMissing} recent flight(s) produced no closing account, so their "
                + "takeover seeds are measurements only",
            // Not blocking. A takeover still works on measurements - that is the
            // point of the fallback - and calling this blocking would stop the
            // thing it exists to protect.
            Blocking = false,
            Fixable = false,
        };

    /// <summary>
    /// Says that moves are recorded rather than enforced.
    /// </summary>
    /// <remarks>
    /// <b>Always reported, never blocking, and never passing.</b> Not a failure of
    /// this machine's setup - it is a property of the product, and a person reading
    /// an envelope's <c>moves</c> list would otherwise reasonably assume it bounds
    /// what an agent may do. Measured rather than assumed: the allow-list passed to
    /// the executor does not refuse a call, and the deny-list would.
    /// </remarks>
    private static DoctorCheck MovesCheck() =>
        new()
        {
            Name = DoctorChecks.Moves,

            // A DISCLOSURE, not a failure. It is reported every time and can never go
            // green, and a line like that renders as something to scroll past unless it
            // says what kind of thing it is.
            Discloses = true,

            // FALSE, deliberately. "Passed" would mean the check found nothing
            // wrong, and what it found is that a bound somebody expects is absent.
            Passed = false,
            Detail = "declared moves are RECORDED, not enforced. A flight declaring 'read' can "
                   + "still edit: the allow-list gg passes to the executor does not refuse a call. "
                   + "What a flight actually did is measured and reported; what it was allowed to "
                   + "do is not a bound.",
            Blocking = false,

            // Nothing on this machine fixes it, and offering a remedy would send
            // somebody looking for a setting that does not exist.
            Fixable = false,
        };

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
