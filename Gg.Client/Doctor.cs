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

    /// <summary>
    /// The version of this executable, against what the control plane published.
    /// </summary>
    /// <remarks>
    /// <b>The third version, and the only one nothing watched.</b> The protocol
    /// has a floor and a 426 that enforces it; the fact vocabulary has a header
    /// printed on purpose; the binary had neither, which is why it is the one
    /// that drifts. It is deliberately NOT blocking - being behind is reported,
    /// never refused, and the 426 stays the only thing in this design that
    /// stops anybody.
    /// </remarks>
    public const string Binary = "binary";
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
    /// are recorded and only partly enforced: the executor's allow-list binds some
    /// tools and not others, so a flight declaring <c>read</c> is refused an edit and
    /// is not stopped from running a shell command that edits anyway. Somebody
    /// reading an envelope will otherwise assume a whole bound exists, and a partial
    /// one presented as none is as misleading as none presented as whole.
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

    /// <summary>Whether this machine can actually invoke an agent.</summary>
    /// <remarks>
    /// <b>A host with no executor passes every other check.</b> It registers,
    /// claims flights, and runs nothing - which from the control plane looks
    /// exactly like a busy runner, because the flights ARE claimed.
    /// <c>ExecutorConfiguration</c>'s own remarks record what that cost the
    /// first time: <i>every runner the product started built a loop whose
    /// executor was null and no flight ever invoked an agent.</i>
    /// </remarks>
    public const string Executor = "executor";

    /// <summary>Whether this machine can reach a forge to clone from.</summary>
    /// <remarks>
    /// Not blocking: plenty of hosts take flights that never clone. Worth
    /// saying anyway, because a pool host missing it looks healthy right up
    /// until a flight needs a repository.
    /// </remarks>
    public const string Forge = "forge";

    /// <summary>Whether this machine maintains a pool.</summary>
    public const string Pool = "pool";
}

/// <summary>
/// What this machine was installed to do, as facts rather than as environment.
/// </summary>
/// <remarks>
/// <para>
/// <b>Passed to the doctor rather than read by it.</b> <c>Gg.Client</c>
/// references only <c>Gg.Contracts</c> - the executor lives in
/// <c>Gg.Runner</c>, and the variables belong to whoever composed the process.
/// That is the same reasoning <c>accountsMissing</c> already rides on.
/// </para>
/// <para>
/// <b>Every field's absent state is ordinary.</b> A laptop is not a pool host.
/// Nothing here is blocking for that reason, and a doctor that failed on a
/// developer's machine would be a verb nobody runs.
/// </para>
/// </remarks>
public sealed record MachineRole
{
    /// <summary>Where the agent binary is, or null when none is configured.</summary>
    public string? ExecutorBinary { get; init; }

    /// <summary>Whether that binary is actually there.</summary>
    /// <remarks>
    /// Separate from the path, because "nobody configured one" and "somebody
    /// configured one that is not there" are two mistakes with two different
    /// fixes - and a typo in a unit file reads identically to a missing install
    /// without it.
    /// </remarks>
    public bool ExecutorPresent { get; init; }

    /// <summary>The forge hosts this machine serves, as configured.</summary>
    public string? ForgeHosts { get; init; }

    /// <summary>Where proposals are opened, as configured.</summary>
    public string? DestinationApis { get; init; }

    /// <summary>The pool endpoint, when this host maintains one.</summary>
    public string? PoolEndpoint { get; init; }

    /// <summary>A machine configured for nothing in particular.</summary>
    public static MachineRole None { get; } = new();

    /// <summary>A machine with an agent binary, present or not.</summary>
    public static MachineRole WithExecutor(string binary, bool present) =>
        new() { ExecutorBinary = binary, ExecutorPresent = present };
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

    /// <summary>
    /// How many checks failed. Disclosures are not among them.
    /// </summary>
    /// <remarks>
    /// <b>Cry wolf, in the tool people run when something is already wrong.</b> A doctor
    /// reporting three failures when one of them can never pass teaches somebody that the
    /// number is inflated - and the next number they discount is a real one.
    /// </remarks>
    public int Failed => Checks.Count(c => c.Outcome == DoctorOutcome.Fail);
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
/// <param name="addressConfigured">
/// Whether <paramref name="controlPlane"/> is a value somebody set, or the
/// built-in localhost fallback.
/// </param>
/// <remarks>
/// <b>The address being a default is a fact about this machine.</b> Unreachable
/// and defaulted is a different sentence from unreachable and configured, and
/// only one of them is about the server. Defaulting to true keeps every existing
/// caller and test saying exactly what they said before.
/// </remarks>
public sealed class Doctor(
    ControlPlaneClient client, ISessionStore sessions, ICredentialStore credentials, Uri controlPlane,
    bool addressConfigured = true)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;
    private readonly ICredentialStore _credentials = credentials;
    private readonly Uri _controlPlane = controlPlane;
    private readonly bool _addressConfigured = addressConfigured;

    /// <param name="accountsMissing">
    /// How many recent flights produced no closing account. Passed in because
    /// gg's evidence lives on the other side of an API call and the doctor does
    /// not go looking; the console knows, and telling it is cheaper than a
    /// second fetch.
    /// </param>
    /// <param name="role">
    /// What this machine was installed to do. <see cref="MachineRole.None"/> is
    /// an ordinary answer and the default, so a caller that has nothing to say
    /// about the machine says nothing.
    /// </param>
    public async Task<DoctorReport> RunAsync(
        int accountsMissing = 0,
        MachineRole? role = null,
        CancellationToken cancellationToken = default)
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

                // WHICH FAILURE THIS IS. An address nobody set is this
                // machine's problem and reads as the server's: the fallback is
                // localhost, so every verb fails with a connection refused and
                // the old sentence sent two people to look at a healthy
                // control plane.
                Detail = _addressConfigured
                    ? $"could not connect to {_controlPlane}: {failure.Message}"
                    : $"could not connect to {_controlPlane}, which is the built-in default "
                    + "because GG_CONTROL_PLANE is not set",
                Blocking = true,

                // Nothing on this machine changes whether a remote service is
                // up. Telling somebody to try is how a support call starts
                // badly - but a variable nobody set is precisely what this
                // machine can fix.
                Fixable = !_addressConfigured,
                Fix = _addressConfigured
                    ? null
                    : "Set GG_CONTROL_PLANE to the address of your control plane.",
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
                // NAMES THE VERB, because "install a newer gg" is true and is
                // not an instruction. The command differs by install shape, and
                // the obvious guess - `dotnet tool update -g` - is wrong on a
                // pool host in a way that reports success and changes nothing.
                // `gg update` knows which shape this is.
                Fix = "gg update - it names the command for this install",
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

        checks.Add(await BinaryCheckAsync(reachable, cancellationToken));
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

        // LAST, and about a different question. Everything above answers "can
        // this machine talk to the control plane"; these answer "can it do the
        // job it was installed for" - which is the question a person stands up
        // a pool host to settle, and the one doctor did not answer.
        checks.AddRange(RoleChecks(role ?? MachineRole.None));

        return new DoctorReport { Checks = checks };
    }

    /// <summary>
    /// What this machine is equipped to do, said out loud.
    /// </summary>
    /// <remarks>
    /// <b>None of it is blocking.</b> A laptop is not a pool host, and an exit
    /// code that failed there would make the verb useless where it is used
    /// most. All of it is fixable, because every one of these is a value on
    /// this machine.
    /// </remarks>
    private static IReadOnlyList<DoctorCheck> RoleChecks(MachineRole role) =>
    [
        role.ExecutorBinary is not { Length: > 0 }
            ? new DoctorCheck
            {
                Name = DoctorChecks.Executor,
                Passed = false,

                // THE CONSEQUENCE, not the variable. "GG_EXECUTOR_BINARY is not
                // set" is true and means nothing to somebody who does not
                // already know what it does.
                Detail = "no agent binary is configured, so this machine will claim flights and "
                       + "never invoke an agent",
                Blocking = false,
                Fixable = true,
                Fix = "Set GG_EXECUTOR_BINARY to the agent binary this machine should run.",
            }
            : !role.ExecutorPresent
            ? new DoctorCheck
            {
                Name = DoctorChecks.Executor,
                Passed = false,

                // The path it looked for IS the diagnosis: without it, a typo
                // in a unit file reads identically to a missing install.
                Detail = $"the configured agent binary is not there: {role.ExecutorBinary}",
                Blocking = false,
                Fixable = true,
                Fix = "Install the agent, or correct GG_EXECUTOR_BINARY.",
            }
            : new DoctorCheck
            {
                Name = DoctorChecks.Executor,
                Passed = true,
                Detail = role.ExecutorBinary,
                Blocking = false,
                Fixable = false,
            },

        role.ForgeHosts is { Length: > 0 } hosts
            ? new DoctorCheck
            {
                Name = DoctorChecks.Forge,
                Passed = true,

                // The hosts, never a credential. This line goes to stdout, and
                // stdout is what a customer pastes into a ticket.
                Detail = role.DestinationApis is { Length: > 0 }
                    ? $"{hosts}, and a destination api is configured"
                    : $"{hosts}, and no destination api is configured, so nothing will be proposed",
                Blocking = false,
                Fixable = false,
            }
            : new DoctorCheck
            {
                Name = DoctorChecks.Forge,
                Passed = false,
                Detail = "no forge is configured, so a flight that needs a repository cannot "
                       + "clone one here",
                Blocking = false,
                Fixable = true,
                Fix = "Set GG_VCS_HOSTS for the forges this machine should serve.",
            },

        role.PoolEndpoint is { Length: > 0 } pool
            ? new DoctorCheck
            {
                Name = DoctorChecks.Pool,
                Passed = true,
                Detail = pool,
                Blocking = false,
                Fixable = false,
            }
            : new DoctorCheck
            {
                Name = DoctorChecks.Pool,
                Passed = false,
                Detail = "no pool endpoint is configured; this host maintains no pool",
                Blocking = false,

                // Not fixable, because most machines are not meant to. A fix
                // offered here would read as something everybody ought to do.
                Fixable = false,
            },
    ];

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
    /// what an agent may do.
    /// <para>
    /// <b>Re-measured, and the old wording was wrong.</b> It said a flight declaring
    /// `read` can still edit, on the strength of a measurement taken with a command
    /// this product does not run - the binary invoked without the flag the bound
    /// rests on. It cannot edit. It can run shell commands, because the bound is per
    /// tool, and the whole of it is contingent on a flag whose mechanism is not
    /// characterised - which is why the runner now proves it at startup and refuses
    /// to take work when it does not hold.
    /// </para>
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
            // SAYS WHAT IT MEANS, not only what is true. A statement about the executor
            // leaves the reader to work out the consequence, and the consequence is the
            // reason this is printed at all.
            Detail = "declared moves are PARTLY enforced, and not enforced as a whole. Measured "
                   + "one tool at a time: a flight declaring 'read' is refused an edit and is NOT "
                   + "stopped from running shell commands, which can edit anyway. So an envelope's "
                   + "moves list is a partial boundary and a full record of intent - what a flight "
                   + "actually did is measured and reported, and what it was allowed to do bounds "
                   + "some of it. The runner proves the bound before it takes any work and "
                   + "refuses to run if it does not hold.",
            Blocking = false,

            // Nothing on this machine fixes it, and offering a remedy would send
            // somebody looking for a setting that does not exist.
            Fixable = false,
        };

    /// <summary>
    /// Where this binary stands against what the control plane published.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never blocking, whatever it finds.</b> Rule 6: being behind is
    /// reported and the 426 remains the only refusal in this design. A blocking
    /// check here would stop somebody working over a number that moves weekly.
    /// </para>
    /// <para>
    /// <b>An oracle that did not answer is said, not smoothed over.</b>
    /// <c>Passed</c> is true only when the versions actually match — an
    /// unreachable control plane leaves this reporting an absence, because a
    /// person told "fine" by a check that never ran is a person who never
    /// updates. The same reasoning as the protocol check one row up, on the
    /// field where the consequence is quietest.
    /// </para>
    /// </remarks>
    private async Task<DoctorCheck> BinaryCheckAsync(
        bool reachable, CancellationToken cancellationToken)
    {
        var current = reachable
            ? await _client.CurrentVersionAsync(cancellationToken)
            : null;

        var standing = VersionStanding.For(GgVersions.Binary, current);

        return new DoctorCheck
        {
            Name = DoctorChecks.Binary,
            Passed = standing.IsReassuring,
            Detail = standing.Kind switch
            {
                VersionStandingKind.Current =>
                    $"{standing.Installed}, which is current",
                VersionStandingKind.Behind =>
                    $"{standing.Installed}, and {standing.Current} is current",
                VersionStandingKind.Unrecognised =>
                    $"{standing.Installed}, which the control plane has never published - it "
                    + $"knows {standing.Current}. Worth asking where this one came from",
                _ => reachable
                    ? $"{standing.Installed}. What is current could not be established, so this "
                    + "may or may not be it"
                    : $"{standing.Installed}. Not checked: the control plane could not be reached",
            },

            // RULE 6, AS A FIELD. Nothing about a version may stop a person.
            Blocking = false,

            // A check that could not run has no fix to offer, for the reason
            // the protocol check gives when the control plane is down: sending
            // somebody to reinstall gg over a network problem costs them time
            // and changes nothing.
            Fixable = standing.Kind == VersionStandingKind.Behind,
            Fix = standing.Kind == VersionStandingKind.Behind
                ? "gg update - it names the command for this install"
                : null,
        };
    }

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
