namespace Gg.Contracts;

/// <summary>
/// Where a secret lives, from the control plane's point of view.
/// </summary>
/// <remarks>
/// <para>
/// One kind, and that is the whole slice. A keychain adapter and a Key Vault
/// adapter are real work protecting against a threat this slice does not
/// address, and an enum carrying unused members that quietly work is how a
/// shortcut gets inherited by the delegation path later.
/// </para>
/// <para>
/// <b>There is deliberately no environment-variable kind, and there never
/// will be.</b> Environment variables leak into child processes, <c>ps</c>
/// output, crash dumps and CI logs. For a product about credential
/// containment it is the one adapter that would undercut the pitch, and
/// "we will be careful" is not a control.
/// </para>
/// </remarks>
/// <summary>
/// What a credential can be FOR. A repository is one of these rather than the only
/// one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap the owner found by using the product:</b> <i>"when i go to add a
/// credential, it immediately asks me for the repository. credentials may be used for
/// other things than repositories."</i> The locator vocabulary already knew better —
/// <see cref="CredentialLocator.ForAgent"/> has sat beside
/// <see cref="CredentialLocator.ForRepo"/> since the credential was first sealed — and
/// only the verb that registers one could not name anything else.
/// </para>
/// <para>
/// <b>NOT THE SAME AXIS AS <see cref="CredentialKinds"/>, which is the thing to keep
/// straight.</b> A kind says HOW the secret is reached — <c>local</c>, and a vault
/// scheme beside it. A subject says WHAT it is for. A credential has both: a
/// <c>local</c> credential for a tracker. Collapsing them would make "where it is" and
/// "what it opens" one question with one answer, and they have different answers.
/// </para>
/// <para>
/// <b>Closed, and refused loudly when unknown</b> — ADR-0027's rule for <c>kind</c> and
/// <c>StrategyKinds</c>' precedent. Each member owes a reserved locator namespace (or,
/// for a repository, the absence of one) and a producer in
/// <see cref="CredentialLocator.For"/>; a subject with neither is a credential filed
/// where nothing resolves, discovered by a flight much later.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class CredentialSubjects
{
    /// <summary>A repository, as its provider spells it.</summary>
    /// <remarks>
    /// <b>The one with no namespace of its own</b>, because it is what is left when no
    /// reserved segment claims the locator — which is why
    /// <see cref="CredentialLocator.ForRepo"/> has to refuse the reserved words rather
    /// than tidy them.
    /// </remarks>
    public const string Repository = "repository";

    /// <summary>An agent, by the key its executor is declared with.</summary>
    public const string Agent = "agent";

    /// <summary>A tracker, by the key an intent host names it with.</summary>
    /// <remarks>
    /// <b>This fleet has had one all along and called it a repository.</b>
    /// <c>GG_INTENT_HOSTS=ado=…|local:jdx/jdnext|…</c> is a hosted tracker
    /// behind a repository-shaped locator, because that was the only shape
    /// <c>gg credential add</c> could make.
    /// </remarks>
    public const string Tracker = "tracker";

    /// <summary>Every subject a credential can be registered for.</summary>
    public static IReadOnlyList<string> All { get; } = [Repository, Agent, Tracker];

    /// <summary>The diagnosis, or null when the subject is one that exists.</summary>
    /// <remarks>
    /// <b>It names every subject there is</b>, because somebody who guessed wrong has
    /// no other way to find out — and because the alternative, narrowing an unknown
    /// subject to a repository, is how this fleet's tracker ended up with a
    /// repository's locator.
    /// </remarks>
    public static string? Refuse(string? subject) =>
        subject is { Length: > 0 } named && All.Contains(named, StringComparer.Ordinal)
            ? null
            : $"'{subject}' is not something a credential can be registered for. "
            + $"gg knows: {string.Join(", ", All)}.";
}

[VocabularyOf(VocabularyFingerprints.Contract)]
public static class CredentialKinds
{
    /// <summary>A file on the machine that registered it. See <see cref="CredentialLocator"/>.</summary>
    public const string Local = "local";

    /// <summary>Every kind that validates. Exactly one.</summary>
    public static IReadOnlyList<string> All { get; } = [Local];
}

/// <summary>
/// What a credential may be used for.
/// </summary>
/// <remarks>
/// <para>
/// Read, and only read. Nothing in this slice writes anywhere, and a scope
/// list that could ask for more would be a promise the rest of the system does
/// not keep - the runner has no write path at all.
/// </para>
/// <para>
/// <b>What is asserted, and what is not.</b> Two things are held today: the
/// only scope that validates is <c>read</c>, and a reference asking for more
/// is refused - here, and again by the control plane. The stronger claim,
/// <i>a write attempt fails at the credential rather than at our API</i>, is
/// NOT asserted and must not be, because nothing fetches anything yet and
/// there is no real token to try it with. A test for it now would assert our
/// own intention and pass forever.
/// </para>
/// <para>
/// It is <b>step 6's criterion</b>: when the runner fetches for the first
/// time, the thing to prove is that a write refused by the provider is
/// refused by the credential's own scope - which is a claim about a token
/// somebody actually minted, and only provable against one.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class CredentialScopes
{
    /// <summary>Read the repository, and nothing else.</summary>
    public const string Read = "read";

    /// <summary>
    /// Push a branch and open a pull request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Registered by the developer, in their own store, and never widened
    /// from anywhere else.</b> An envelope declares that a flight MAY land
    /// somewhere; it cannot grant the ability to. A write destination against a
    /// read-only credential fails at the credential, with a diagnosis - which is
    /// the layering model reaching across the boundary, because a control plane
    /// that could escalate a credential would make the customer's own store
    /// advisory.
    /// </para>
    /// <para>
    /// Slice one refused this scope at the edge, correctly: nothing could write
    /// and a scope wider than read was a request nobody had a use for. This is
    /// that changing, deliberately, with the other control arriving in the same
    /// step.
    /// </para>
    /// </remarks>
    public const string Write = "write";

    /// <summary>Every scope that validates.</summary>
    public static IReadOnlyList<string> All { get; } = [Read, Write];

    /// <summary>Whether this set of scopes permits writing.</summary>
    /// <remarks>
    /// Declared once, because the runner asks it before pushing and the control
    /// plane asks it before admitting. Two derivations of "can this write" is
    /// how one side comes to believe a flight may land when it may not.
    /// </remarks>
    public static bool AllowWrite(IReadOnlyList<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        return scopes.Contains(Write, StringComparer.Ordinal);
    }
}

/// <summary>
/// The <c>local:</c> locator format, declared once for everyone who touches it.
/// </summary>
/// <remarks>
/// <para>
/// gg writes the file, the control plane stores the string, and the runner
/// reads the file back. Three places, so the rule lives here: two derivations
/// that agree today is how a runner ends up looking for a file the CLI never
/// wrote.
/// </para>
/// <para>
/// The charset constrains the SHAPE and not the intent. A locator is short,
/// lowercase and path-shaped; a bearer value is long and mixed-case. That
/// stops the accident, not somebody determined to paste a token into the wrong
/// prompt - and the control plane's absence scan is what covers the rest.
/// </para>
/// <para>
/// <b>It declares the contract's fingerprint because it now carries a vocabulary.</b>
/// <see cref="ReservedSegments"/> arrived in slice sixty-four step 4 and made this a
/// closed set somebody can add to — so adding one has to move a ledger and make
/// somebody think, which is the whole mechanism. Two of its constants are exempt from
/// the totality scan with reasons: a prefix and a URI scheme are not members of this
/// set, and one of them belongs to the other axis entirely.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class CredentialLocator
{
    /// <summary>Every local locator begins with this.</summary>
    public const string LocalPrefix = "local:";

    /// <summary>
    /// The longest locator this contract accepts, prefix included.
    /// </summary>
    /// <remarks>
    /// Bounded so a pasted credential does not fit. Provider tokens are longer
    /// than this, and a repository slug is very much shorter.
    /// </remarks>
    public const int MaxLength = 96;

    /// <summary>The diagnosis, or null when the locator is well formed.</summary>
    /// <summary>
    /// The first segment reserved for agents' own credentials, so a repository
    /// slug cannot derive the same file.
    /// </summary>
    /// <remarks>
    /// <c>ForRepo</c> reduces a slug through the same character set a locator
    /// validates, so a repository called <c>agent/claude</c> derived the
    /// identical string as the claude agent's token - one file, whichever was
    /// written last. The two derivations are disjoint only if one refuses the
    /// other's namespace, and the repository side is the one that takes prose.
    /// </remarks>
    public const string AgentSegment = "agent";

    /// <summary>
    /// The first segment reserved for trackers' credentials, for
    /// <see cref="AgentSegment"/>'s reason exactly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It arrived late and the comment above predicted it</b> — <i>"a repository's
    /// locator named for an agent would read the credential a tracker owns"</i> was
    /// written when a tracker had no namespace of its own, which is why this fleet's
    /// tracker credential is <c>local:jdx/jdnext</c>: a repository-shaped locator,
    /// because that was the only shape there was.
    /// </para>
    /// <para>
    /// <b>Every reserved segment costs <see cref="ForRepo"/> a refusal</b>, and the
    /// refusal has to happen after the slug is reduced — <c>Tracker/x</c> lowercases
    /// into this word, so a guard comparing the raw input passes it through.
    /// </para>
    /// </remarks>
    public const string TrackerSegment = "tracker";

    /// <summary>Every segment a repository slug may not begin with.</summary>
    /// <remarks>
    /// <b>Derived, so adding a subject cannot forget to reserve its namespace.</b> The
    /// alternative is a second list that agrees today, which is the hazard this file is
    /// written to prevent.
    /// </remarks>
    public static IReadOnlyList<string> ReservedSegments { get; } = [AgentSegment, TrackerSegment];

    /// <summary>The locator an agent's own credential lives under.</summary>
    /// <remarks>
    /// <b>Refused rather than reduced.</b> An agent's name is a key - the same
    /// key <c>GG_EXECUTOR_BINARY</c> declares it with - and a key that does not
    /// validate as a segment is a mistake to name, not a value to tidy: tidying
    /// would let the console and the executor derive two locators from two
    /// spellings of one agent.
    /// </remarks>
    public static string ForTracker(string tracker)
    {
        var locator = $"{LocalPrefix}{TrackerSegment}/{tracker}";

        return Validate(locator) is { } refused
            ? throw new ArgumentException(
                $"'{tracker}' is not a name a tracker's locator can carry: {refused}",
                nameof(tracker))
            : locator;
    }

    /// <summary>The locator for a subject, derived in the one place that knows how.</summary>
    /// <remarks>
    /// <para>
    /// <b>ONE MAP, because two would be the hazard this file exists to prevent:</b>
    /// <i>"two derivations that agree today is how a runner ends up looking for a file
    /// the CLI never wrote."</i> A caller that switched on the subject itself would be
    /// the second derivation, and a new subject would then need finding in two places
    /// — one of which has no test that knows it is missing.
    /// </para>
    /// <para>
    /// <b>An unknown subject throws rather than falling back to a repository.</b>
    /// Narrowing it would file the credential somewhere nothing resolves, and a flight
    /// would report it absent much later with nothing pointing here.
    /// </para>
    /// </remarks>
    public static string For(string subject, string named) =>
        subject switch
        {
            CredentialSubjects.Repository => ForRepo(named),
            CredentialSubjects.Agent => ForAgent(named),
            CredentialSubjects.Tracker => ForTracker(named),
            _ => throw new ArgumentException(
                CredentialSubjects.Refuse(subject) ?? $"'{subject}' is not a subject.",
                nameof(subject)),
        };

    /// <summary>What a locator is for, read back out of it.</summary>
    /// <remarks>
    /// <para>
    /// <b>A repository is what is left when no reserved namespace claims it</b>, which
    /// is exactly why <see cref="ForRepo"/> must refuse the reserved words on the way
    /// in — without that refusal this answer would be wrong for any repository named
    /// after one.
    /// </para>
    /// <para>
    /// <b>It opens nothing and reads no disk.</b> A sentence about a credential must
    /// never be a reason to resolve one, which is the rule <c>Holds</c> already
    /// carries.
    /// </para>
    /// </remarks>
    public static string SubjectOf(string locator)
    {
        ArgumentException.ThrowIfNullOrEmpty(locator);

        foreach (var reserved in ReservedSegments)
        {
            if (locator.StartsWith($"{LocalPrefix}{reserved}/", StringComparison.Ordinal))
            {
                return reserved == AgentSegment
                    ? CredentialSubjects.Agent
                    : CredentialSubjects.Tracker;
            }
        }

        return CredentialSubjects.Repository;
    }

    public static string ForAgent(string provider)
    {
        var locator = $"{LocalPrefix}{AgentSegment}/{provider}";
        if (Validate(locator) is { } refused)
        {
            throw new ArgumentException(
                $"'{provider}' is not a name an agent's locator can carry: {refused}",
                nameof(provider));
        }

        return locator;
    }

    /// <summary>The scheme a reference this machine READS carries.</summary>
    /// <remarks>
    /// <b>Named once, because this file's own rule says so.</b> The locator format
    /// is declared here <i>"so the rule lives in one place: two derivations that
    /// agree today is how a runner ends up looking for a file the CLI never
    /// wrote"</i> — and the vault scheme was spelled inline in two other files in
    /// this assembly before anything needed a third.
    /// </remarks>
    public const string VaultScheme = "keyvault://";

    /// <summary>
    /// The rule for a place an AGENT's credential may be named: a vault reference
    /// this machine reads, or the local file in the agent namespace. Null means
    /// well formed; anything else is the reason, with a subject for the caller to
    /// put in front of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Shared, because three places now say it.</b> A strategy declares one for
    /// a pool's members, a machine declares one for itself, and both are read by
    /// the same store — so a rule written twice is a document a person can apply
    /// and a runner then refuses.
    /// </para>
    /// <para>
    /// <b>THE REASON NEVER REPEATS THE INPUT.</b> A refusal here is exactly the
    /// moment somebody pasted a token where a name belongs, so quoting it would
    /// print the secret into whatever showed the refusal — a console, a gate, a
    /// flight log. The subject the caller adds is a setting's name or a document
    /// key, and those are the only parts safe to say.
    /// </para>
    /// </remarks>
    public static string? ValidateAgentReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return "is blank. It says WHERE an agent's credential is, and nowhere is not a "
                 + "place - leave it out for the local file every machine derives.";
        }

        var named = reference.Trim();

        if (named.StartsWith(VaultScheme, StringComparison.Ordinal))
        {
            var rest = named[VaultScheme.Length..];
            var slash = rest.IndexOf('/', StringComparison.Ordinal);

            return slash > 0 && slash < rest.Length - 1 && !rest.Any(char.IsWhiteSpace)
                ? null
                : $"names a {VaultScheme} reference that is not a vault and a secret. The shape "
                + "is the vault's host, a slash, and the secret's name.";
        }

        if (named.StartsWith(LocalPrefix, StringComparison.Ordinal))
        {
            if (Validate(named) is { } refused)
            {
                return $"names a '{LocalPrefix}' locator that is not well formed: {refused}";
            }

            // ForRepo reduces a repository slug through this same character set,
            // so the two derivations are disjoint only while one refuses the
            // other's namespace. A repository's locator named for an agent would
            // read the credential a tracker owns.
            return named.StartsWith($"{LocalPrefix}{AgentSegment}/", StringComparison.Ordinal)
                ? null
                : $"names a local locator outside '{LocalPrefix}{AgentSegment}/', which is where "
                + "an agent's own credential lives.";
        }

        return $"is neither a {VaultScheme} reference nor a '{LocalPrefix}' locator. It says "
             + "WHERE the credential is and never what it is - put the secret in a vault, or "
             + "run `gg credential add`, and name it here.";
    }

    public static string? Validate(string? locator)
    {
        if (string.IsNullOrEmpty(locator))
        {
            return "A credential locator says where the secret lives. This one is empty.";
        }

        if (locator.Length > MaxLength)
        {
            return $"A credential locator is at most {MaxLength} characters, and this one is "
                 + $"{locator.Length}. A locator names a place; it does not carry a value.";
        }

        if (!locator.StartsWith(LocalPrefix, StringComparison.Ordinal))
        {
            return $"'{locator}' does not begin with '{LocalPrefix}'. Local is the only kind of "
                 + "credential this protocol has.";
        }

        var body = locator[LocalPrefix.Length..];
        if (body.Length == 0)
        {
            return $"'{locator}' names no place after '{LocalPrefix}'.";
        }

        foreach (var segment in body.Split('/'))
        {
            if (segment.Length == 0 || !char.IsAsciiLetterOrDigit(segment[0]))
            {
                // A segment starting with a dot is how ".." gets in, and ".."
                // is how a locator becomes a path anywhere on the machine.
                return $"'{locator}' has a segment that does not start with a lowercase letter or digit.";
            }

            if (segment.Any(c => !(char.IsAsciiDigit(c) || (c >= 'a' && c <= 'z') || c is '.' or '-' or '_')))
            {
                return $"'{locator}' contains something other than lowercase letters, digits, "
                     + "'.', '-', '_' and '/'.";
            }
        }

        return null;
    }

    /// <summary>The locator for a repository, derived the same way everywhere.</summary>
    /// <remarks>
    /// Lowercased and reduced to the accepted charset, so the same repository
    /// spelled two ways is one credential rather than two.
    /// </remarks>
    public static string ForRepo(string repoSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoSlug);

        var reduced = new string([.. repoSlug.ToLowerInvariant()
            .Select(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'z') || c is '.' or '-' or '_' or '/'
                ? c
                : '-')]);

        // Collapse anything that would produce an empty segment, so a slug
        // like "acme//widgets" cannot become a locator this contract refuses.
        var segments = reduced.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.TrimStart('.', '-', '_'))
            .Where(s => s.Length > 0)
            .ToList();

        // EVERY RESERVED NAMESPACE, refused here rather than shared. See
        // AgentSegment: a slug reduces through the same alphabet a locator
        // validates, so this is the only place the derivations can be kept apart -
        // and the comparison is against the REDUCED first segment, because
        // `Tracker/x` lowercases into a reserved word and a guard reading the raw
        // input would pass it through.
        if (segments.FirstOrDefault() is { } first
         && ReservedSegments.Contains(first, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"'{repoSlug}' begins with '{first}', which is reserved for {first}s' own "
              + $"credentials - a repository under that owner would share a file with a "
              + $"{first}'s token. Reserved: {string.Join(", ", ReservedSegments)}.",
                nameof(repoSlug));
        }

        var body = string.Join('/', segments);
        if (body.Length == 0)
        {
            throw new ArgumentException(
                $"'{repoSlug}' reduces to nothing a locator could name.", nameof(repoSlug));
        }

        var locator = LocalPrefix + body;
        return locator.Length > MaxLength ? locator[..MaxLength].TrimEnd('/', '.', '-', '_') : locator;
    }
}

/// <summary>
/// Where a secret is, who it acts as, and what it may do. Never the secret.
/// </summary>
/// <remarks>
/// <para>
/// The claim at the heart of the product, as a type. Constitution Article VIII: the
/// control plane stores references and facts, never secrets. Four members, and
/// none of them can hold one - which is asserted over the shape, so adding a
/// fifth fails the build.
/// </para>
/// <para>
/// <see cref="Identity"/> is a FACT the developer supplied: which account at
/// the provider this credential acts as. It is what makes a flight log
/// answerable later - "the runner read the repository as acme-bot" - and it is
/// not a secret, because knowing the name of an account grants nothing.
/// </para>
/// </remarks>
[PinnedId("6b4d3b0e-5f47-4b8c-97b8-6a6b0d4b4e51")]
public sealed record CredentialReference
{
    /// <summary>How the secret is stored. <c>local</c>, and only <c>local</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Where it is, in the form <see cref="CredentialLocator"/> declares.</summary>
    public required string Locator { get; init; }

    /// <summary>Which account at the provider this credential acts as.</summary>
    public required string Identity { get; init; }

    /// <summary>What it may do. Read, and only read.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>
    /// The diagnosis, or null when there is nothing wrong.
    /// </summary>
    /// <remarks>
    /// A sentence rather than a bool, for the same reason
    /// <see cref="FlightIntent.Validate"/> returns one: Article XI asks for a
    /// diagnosis, and "invalid credential" tells whoever hit it nothing.
    /// The control plane refuses independently - this is the client's copy of
    /// one rule, not the only gate.
    /// </remarks>
    public static string? Validate(CredentialReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (!CredentialKinds.All.Contains(reference.Kind))
        {
            return $"Unknown credential kind '{reference.Kind}'. Expected one of: "
                 + string.Join(", ", CredentialKinds.All) + ".";
        }

        if (CredentialLocator.Validate(reference.Locator) is { } locatorProblem)
        {
            return locatorProblem;
        }

        if (string.IsNullOrWhiteSpace(reference.Identity))
        {
            return "A credential reference names the account it acts as, and this one does not. "
                 + "Without it a flight log cannot say who read the repository.";
        }

        if (reference.Scopes.Count == 0)
        {
            // Article XI. Defaulting an empty list to read would make "scopes
            // are requested read-only" true by our own generosity rather than
            // by what the caller asked for.
            return "A credential reference requests at least one scope. This one requests none.";
        }

        var wider = reference.Scopes.Where(s => !CredentialScopes.All.Contains(s)).ToList();
        return wider.Count > 0
            ? $"Scope '{wider[0]}' is not one this protocol grants. Expected only: "
              + string.Join(", ", CredentialScopes.All) + "."
            : null;
    }
}

/// <summary>
/// Registers a reference to a credential the developer already stored locally.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no field here capable of carrying secret material, and that is
/// asserted over the type's shape rather than intended.</b> Adding one fails
/// the build in <c>CredentialContainmentTests</c>.
/// </para>
/// <para>
/// No tenant id, for the same reason nothing else here has one: the caller
/// already is a tenant, and an endpoint that accepted one would be an endpoint
/// somebody could name a different one to.
/// </para>
/// </remarks>
[PinnedId("f0b2c8a4-3d1e-4f52-8a67-9c0d5e7b1a33")]
public sealed record CredentialRegistrationRequest
{
    /// <summary>
    /// What this credential is for, as a person would name it: a repository slug, an
    /// agent, a tracker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It said "the repository this credential is for" and was <c>required</c></b>,
    /// so until slice sixty-four step 4 every registration asserted the credential was
    /// for a repository whether or not it was. That is how this fleet's tracker
    /// credential became <c>local:jdx/jdnext</c>.
    /// </para>
    /// <para>
    /// <b>Kept rather than deleted, and it was close.</b> Nothing decides anything on
    /// this value — the lease grant keys on the locator, and the control plane never
    /// reads it back — so removing it would have been cheaper. What keeps it is
    /// SPELLING: <see cref="CredentialLocator.ForRepo"/> lowercases and reduces, so the
    /// locator cannot give back <c>Acme/Widgets</c>, and a list that shows
    /// <c>acme/widgets</c> instead is one somebody has to translate.
    /// </para>
    /// <para>
    /// <b>WHICH kind of thing it names is read from the locator</b>
    /// (<see cref="CredentialLocator.SubjectOf"/>) rather than carried beside it. Two
    /// members that both answer "what is this for" is two that can disagree, and the
    /// locator is the one every other decision already keys on.
    /// </para>
    /// </remarks>
    public required string For { get; init; }

    /// <summary>Where the secret is, who it acts as, what it may do.</summary>
    public required CredentialReference Reference { get; init; }
}

/// <summary>What the control plane recorded. Still a reference.</summary>
[PinnedId("2c9a7f13-8b04-4a6e-9d5f-1e3b6c8a0d47")]
public sealed record CredentialRegistered
{
    public required string CredentialId { get; init; }

    /// <summary>The reference as stored, so gg can see what was kept.</summary>
    public required CredentialReference Reference { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}

/// <summary>One registered credential, as a person reads it.</summary>
[PinnedId("9e57b21c-6a3d-4c08-b1f9-4d2e7a05c8b6")]
public sealed record CredentialSummary
{
    public required string CredentialId { get; init; }

    /// <summary>
    /// What it is for, as the person who registered it named it. See
    /// <see cref="CredentialRegistrationRequest.For"/> — the two carry one meaning, so
    /// a rename that moved one and not the other would leave a reader deriving the same
    /// fact from two members that disagree about what it is.
    /// </summary>
    public required string For { get; init; }

    public required CredentialReference Reference { get; init; }

    /// <summary>How a document spells whoever registered it.</summary>
    /// <remarks>
    /// <para>
    /// <b>A credential belongs to a person</b>, and until this member there was
    /// no way for a reader to tell whose. The doctor asserted that EVERY
    /// credential in the tenant resolves on the machine it is running on - fine
    /// while a tenant had one developer, and wrong the day it has two: their
    /// secret is on their laptop, where it belongs.
    /// </para>
    /// <para>
    /// <b>The subject, never the principal id</b> - the board page's rule, for
    /// its reason: an id is the control plane's own bookkeeping, and a subject
    /// is how a person is named outside it.
    /// </para>
    /// <para>
    /// <b>Absent from an older control plane, and absent is not "somebody
    /// else's".</b> A reader that skipped what it was not told about would stop
    /// reporting the thing it exists to report.
    /// </para>
    /// </remarks>
    public string? ReferencedBySubject { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}

/// <summary>Every credential this tenant has registered.</summary>
/// <remarks>
/// A store you cannot inspect is a store people work around, and a reference
/// nobody can see is one nobody can tell is broken.
/// </remarks>
[PinnedId("4a1c0d68-72b5-4e39-8f27-5b9e3c6a71d0")]
public sealed record CredentialList
{
    public required IReadOnlyList<CredentialSummary> Credentials { get; init; }
}

/// <summary>A credential the control plane no longer holds a reference to.</summary>
/// <remarks>
/// The reference comes back so gg can delete the local secret it pointed at.
/// Without it the caller would have to remember which file a credential id
/// belonged to, and a store you cannot clean is the other half of one you
/// cannot inspect.
/// </remarks>
[PinnedId("7d3e9b05-1f46-4a72-83c1-0e5a8d2b64f9")]
public sealed record CredentialRemoved
{
    public required string CredentialId { get; init; }

    public required CredentialReference Reference { get; init; }
}

/// <summary>
/// A runner could not resolve a credential, and says which one.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0004 named this failure before it existed: <i>secret-reference
/// indirection fails opaquely. A runner that cannot read a vault produces a
/// stalled flight that looks like a broken product. Diagnostics for this are a
/// feature, not logging.</i>
/// </para>
/// <para>
/// So it is a wire type rather than a string in a log: it carries the
/// reference - which one, which locator, acting as whom - and what went wrong,
/// and the control plane records it on the flight log where somebody will
/// find it. The reference is the same type as everywhere else, so this path
/// cannot carry a secret either.
/// </para>
/// </remarks>
[PinnedId("b58c1a90-4e72-4d16-9f3b-2a7c05e84d1f")]
public sealed record CredentialResolutionFailure
{
    /// <summary>The credential that could not be resolved.</summary>
    public required CredentialReference Reference { get; init; }

    /// <summary>What went wrong, in a sentence somebody can act on.</summary>
    public required string Problem { get; init; }
}
