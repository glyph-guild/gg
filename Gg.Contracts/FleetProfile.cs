namespace Gg.Contracts;

/// <summary>What an enrolled machine may be for: taking flights, and maintaining a pool.</summary>
/// <remarks>
/// <b>Closed at two, and <see cref="Maintain"/> is the one that matters.</b> A
/// runner whose profile says maintain is a resident - the only kind that may
/// mint pool members (slice forty-three, rule 20). Any runner may run.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ProfileRoles
{
    /// <summary>It claims flights.</summary>
    public const string Run = "run";

    /// <summary>It keeps a pool warm, and so may mint members.</summary>
    public const string Maintain = "maintain";

    public static IReadOnlyList<string> All { get; } = [Run, Maintain];
}

/// <summary>
/// What an enrolled machine is: an airspace document, <c>airspace/fleet/&lt;name&gt;.yaml</c>,
/// applied and governed like a strategy (ADR-0025 section 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>It names, and the machine resolves</b> (slice forty-three, rule 14). An
/// <see cref="Agent"/> is a name, never a path to a binary; a
/// <see cref="Credentials"/> entry is a reference, never a secret. Which binary
/// and which secret stay the machine's to find, and <c>executor-binary</c>,
/// <c>intent-readers</c>, <c>pool-endpoint</c> and the <c>accept-*</c> flags
/// stay never-offerable.
/// </para>
/// <para>
/// <b>Its environment is what the lease matches</b> (rule 16): a runner enrolled
/// under a profile advertises at most <c>environment=&lt;Environment&gt;</c>,
/// whatever it says on its heartbeat. Verification can only subtract.
/// </para>
/// </remarks>
[PinnedId("5d1e8a37-4c26-4b9f-8e03-a7f2c9b6d451")]
public sealed record FleetProfile
{
    /// <summary>One or more of <see cref="ProfileRoles"/>.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>The charted environment a machine under this profile furnishes.</summary>
    public required string Environment { get; init; }

    /// <summary>The agent it runs, by name - never a path. Null for a machine that runs none.</summary>
    public string? Agent { get; init; }

    /// <summary>The forges it serves, in <c>vcs-hosts</c>' spelling: <c>key=host</c>.</summary>
    public IReadOnlyList<string> Forges
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>Where it may land work, in <c>destination-apis</c>' spelling: <c>key=api</c>.</summary>
    public IReadOnlyList<string> Destinations
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>
    /// Where machines under it ask what they look like from outside, in
    /// <c>stun-servers</c>' spelling: <c>stun:host:port</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The tenant's to choose, and nobody's to default.</b> gg will not put a
    /// relay in its source - the well-known public ones are run by the companies
    /// this binary may not name, and a default would point every runner in every
    /// deployment at somebody's free service, on a path carrying the shape of a
    /// customer's private network. A pool member gets them from its strategy;
    /// this is the same sentence for the machine a profile describes.
    /// </para>
    /// <para>
    /// <b>Measured on vmlinux002 (S43.8-01).</b> Without them the agent-login
    /// ceremony - the remedy a bring-up gate names - reached the runner and then
    /// ended in "no route between them was found", because host candidates alone
    /// do not cross a NAT. An enrolled machine could not answer its own bring-up
    /// ask, and the way through was to set a machine setting by hand.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Relays
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>
    /// The trackers machines under it READ work items from, in
    /// <c>intent-hosts</c>' spelling: <c>key=host|reference</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The gap this closes is an agent with no tool.</b>
    /// <c>IntentConfiguration</c> states it: a flight about a work item reaches
    /// an agent that cannot read one, because <c>--strict-mcp-config</c> with no
    /// <c>--mcp-config</c> removes the operator's own servers and leaves nothing
    /// behind. No runner in the fleet declares one, so no fleet machine can read
    /// the ticket its flight is about.
    /// </para>
    /// <para>
    /// <b>Keyed by provider</b>, which is what a ticket names, where
    /// <see cref="Triage"/> is keyed by a destination id. The two lists are that
    /// difference and not a style.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Trackers
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>
    /// The trackers machines under it WRITE work items to, in
    /// <c>tracker-apis</c>' spelling: <c>key=api|reference</c>.
    /// </summary>
    /// <remarks>
    /// <b>Only a machine that triages has one</b>, which is
    /// <c>TrackerConfiguration</c>'s sentence: the same binary runs on a machine
    /// that triages and one that never will, and absence is ordinary rather than
    /// degraded.
    /// </remarks>
    public IReadOnlyList<string> Triage
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>Whether it sweeps the tenant's watches when idle.</summary>
    public bool Sweeps { get; init; }

    /// <summary>The credentials it needs, as references its own sources resolve - never a value.</summary>
    public IReadOnlyList<string> Credentials
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>The label the lease matches a runner under this profile by.</summary>
    public static string LabelFor(FleetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return $"environment={profile.Environment}";
    }

    /// <summary>
    /// The schema's own rule, shared so gg and the control plane cannot disagree
    /// about what a valid profile is. Null means valid; anything else is the refusal.
    /// </summary>
    public static string? Validate(FleetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Roles.Count == 0)
        {
            return "This profile names no role, so a machine under it would do nothing. Say "
                 + $"roles: [{ProfileRoles.Run}], [{ProfileRoles.Maintain}] or both.";
        }

        if (profile.Roles.FirstOrDefault(r => !ProfileRoles.All.Contains(r, StringComparer.Ordinal))
            is { } unknown)
        {
            return $"'{unknown}' is not a role a machine can have. A machine may "
                 + $"{string.Join(" and ", ProfileRoles.All)}.";
        }

        if (profile.Roles.Distinct(StringComparer.Ordinal).Count() != profile.Roles.Count)
        {
            return "This profile names a role twice. Each is said once.";
        }

        if (!IsWord(profile.Environment))
        {
            return $"environment is '{profile.Environment}', and an environment is one word - "
                 + "the charted name a runner under this profile advertises as environment=<name>.";
        }

        // RULE 14: A NAME, NEVER A PATH. The binary is the machine's to find;
        // a profile that could point every machine under it at a path could
        // point them at anything that path holds.
        if (profile.Agent is { } agent && !IsWord(agent))
        {
            return $"agent is '{agent}', and a profile names an agent, never a binary. Write "
                 + "the agent's name - claude - and let each machine find its own binary.";
        }

        foreach (var forge in profile.Forges)
        {
            if (Pair(forge) is null)
            {
                return $"forges has '{forge}', and a forge is written key=host, as vcs-hosts is.";
            }
        }

        foreach (var destination in profile.Destinations)
        {
            if (Pair(destination) is null)
            {
                return $"destinations has '{destination}', and a destination is written key=api, "
                     + "as destination-apis is.";
            }
        }

        // A SCHEME IS REQUIRED rather than assumed, which is the rule the
        // machine's own reader already has: `stun:` and `stuns:` are what a peer
        // connection understands, and quietly prefixing one would turn a typo
        // into a server nobody meant. Refused here rather than dropped, because
        // a document a person wrote and a gate approved should not lose entries
        // silently.
        foreach (var relay in profile.Relays)
        {
            if (!IsRelay(relay))
            {
                return $"relays has '{relay}', and a relay is written stun:host:port or "
                     + "stuns:host:port - the scheme is required, never assumed.";
            }
        }

        // THE SAME REFUSAL AS `credentials`, and for the same one mistake: a
        // tracker entry carries its reference inline, which is the first place
        // somebody will write a token into a document the whole tenant reads.
        // A tracker with NO credential is refused too - a machine told where to
        // write with nothing to open it is an incomplete document, and saying so
        // here is cheaper than a bring-up gate explaining it later.
        foreach (var (field, entries) in
                 (IReadOnlyList<(string, IReadOnlyList<string>)>)
                 [("trackers", profile.Trackers), ("triage", profile.Triage)])
        {
            foreach (var tracker in entries)
            {
                if (!IsTracker(tracker))
                {
                    return $"{field} has '{tracker}', and a tracker is written "
                         + "key=host|reference - the host says where it is and the reference says "
                         + "where its secret is, which is never the secret itself.";
                }
            }
        }

        // RULE 14: A REFERENCE, NEVER A SECRET. A reference names its source by
        // scheme - local:, keyvault:// - and a bare value is refused rather
        // than guessed at, because the one mistake this must never allow is a
        // secret written into a document the whole tenant can read.
        foreach (var credential in profile.Credentials)
        {
            if (!IsReference(credential))
            {
                return "credentials has an entry that is not a reference. A profile names where "
                     + "a secret is - local:<name> or keyvault://<vault-host>/<secret> - never "
                     + "the secret itself, and the value is not repeated here in case it was one.";
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="proposed"/> lets a machine do anything
    /// <paramref name="prior"/> did not, and which field says so - or null for
    /// a tightening, which applies at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In the contract, so gg and the control plane read one answer</b> -
    /// the strategy's comparator lives only on the control plane, and gg says
    /// so every time it orders one. A profile is new enough not to repeat that.
    /// </para>
    /// <para>
    /// <b>More reach is a widening; a different reach is too.</b> A role, a
    /// forge, a destination or a credential added; sweeping turned on; and any
    /// change of environment or agent - neither is a subset of the other, and a
    /// machine that now furnishes a different environment takes different work.
    /// Removing any of them tightens.
    /// </para>
    /// </remarks>
    public static (string Field, string Because)? Widening(FleetProfile prior, FleetProfile proposed)
    {
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(proposed);

        if (Added(prior.Roles, proposed.Roles) is { } role)
        {
            return ("roles", $"it adds the role '{role}', so every machine under it may do more.");
        }

        if (!string.Equals(prior.Environment, proposed.Environment, StringComparison.Ordinal))
        {
            return ("environment",
                $"it moves every machine under it from '{prior.Environment}' to "
              + $"'{proposed.Environment}', and a different environment takes different work.");
        }

        if (!string.Equals(prior.Agent, proposed.Agent, StringComparison.Ordinal)
            && proposed.Agent is not null)
        {
            return ("agent", $"it has every machine under it run '{proposed.Agent}'.");
        }

        if (Added(prior.Forges, proposed.Forges) is { } forge)
        {
            return ("forges", $"it adds the forge '{forge}' to every machine under it.");
        }

        if (Added(prior.Destinations, proposed.Destinations) is { } destination)
        {
            return ("destinations", $"it lets every machine under it land work at '{destination}'.");
        }

        // READING A TRACKER WIDENS AS MUCH AS WRITING ONE. Both point a machine
        // at a host and hand it a credential to open it, which is what the forge
        // and destination rows above are about; neither is stun-servers, which
        // is data that grants nothing.
        if (Added(prior.Trackers, proposed.Trackers) is { } tracker)
        {
            return ("trackers", $"it has every machine under it read work items from '{tracker}'.");
        }

        if (Added(prior.Triage, proposed.Triage) is { } triage)
        {
            return ("triage", $"it lets every machine under it write work items to '{triage}'.");
        }

        // RELAYS ARE NOT A WIDENING, and that is the offer rule's decision
        // rather than a new one: stun-servers is in OfferableKeys.Unwatched,
        // because a wrong relay degrades a connection while a wrong forge host
        // fetches code from somewhere nobody chose. A profile that only adds a
        // relay applies at once, as an offer of one does.
        if (!prior.Sweeps && proposed.Sweeps)
        {
            return ("sweeps", "it has every machine under it sweep the tenant's watches.");
        }

        if (Added(prior.Credentials, proposed.Credentials) is { } credential)
        {
            return ("credentials", $"it has every machine under it read '{credential}'.");
        }

        return null;

        static string? Added(IReadOnlyList<string> before, IReadOnlyList<string> after) =>
            after.FirstOrDefault(a => !before.Contains(a, StringComparer.Ordinal));
    }

    private static bool IsWord(string? value) =>
        value is { Length: > 0 and <= 64 }
        && value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_')
        && char.IsAsciiLetterLower(value[0]);

    private static (string Key, string Value)? Pair(string value)
    {
        var at = value.IndexOf('=', StringComparison.Ordinal);
        return at > 0 && at < value.Length - 1 && !value.Any(char.IsWhiteSpace)
            ? (value[..at], value[(at + 1)..])
            : null;
    }

    /// <summary>Whether a relay is written with a scheme a peer connection understands.</summary>
    /// <remarks>
    /// <b>Public, and shared with the machine's own reader</b>, so a profile and
    /// a <c>stun-servers</c> value cannot disagree about what a relay is. The
    /// two readers differ in what they do with a bad one - this refuses the
    /// document, that drops the entry - and agreeing on the question is the part
    /// that matters.
    /// </remarks>
    public static bool IsRelay(string value) =>
        value is { Length: > 0 }
        && !value.Any(char.IsWhiteSpace)
        && (value.StartsWith("stun:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("stuns:", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether <paramref name="value"/> is <c>key=host|reference</c>: a key, a
    /// host, and a reference that names where a secret is rather than being one.
    /// </summary>
    public static bool IsTracker(string value)
    {
        if (value is not { Length: > 0 } || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var equals = value.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0 || equals == value.Length - 1)
        {
            return false;
        }

        // THE LAST BAR, not the first: a host may carry one and a reference may
        // not, so splitting from the right is what keeps a url intact.
        var bar = value.LastIndexOf('|');
        if (bar < 0)
        {
            return false;
        }

        var host = value[(equals + 1)..bar];

        return host.Length > 0 && bar > equals && IsReference(value[(bar + 1)..]);
    }

    private static bool IsReference(string value) =>
        !value.Any(char.IsWhiteSpace)
        && (value.StartsWith("local:", StringComparison.Ordinal) && value.Length > "local:".Length
            || value.StartsWith("keyvault://", StringComparison.Ordinal)
               && value.IndexOf('/', "keyvault://".Length) is > 0 and var slash
               && slash < value.Length - 1);
}

/// <summary>One applied fleet profile, as the read side serves it.</summary>
[PinnedId("e8b3c1f2-6a4d-4e9b-b7c5-3f1a2d8e9c60")]
public sealed record FleetProfileState
{
    /// <summary>The topology name the profile was applied to.</summary>
    public required string Name { get; init; }

    /// <summary>The per-name version in force, e.g. v2.</summary>
    public required string Version { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    public required FleetProfile Profile { get; init; }
}

/// <summary>Every fleet profile in force for the tenant.</summary>
[PinnedId("2a9f6d4b-8c1e-4f73-a5b2-9d0e7c3f1b84")]
public sealed record FleetProfileList
{
    public required IReadOnlyList<FleetProfileState> Profiles { get; init; }
}
