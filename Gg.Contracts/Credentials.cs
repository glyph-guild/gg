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
public static class CredentialScopes
{
    /// <summary>Read the repository, and nothing else.</summary>
    public const string Read = "read";

    /// <summary>Every scope that validates. Exactly one.</summary>
    public static IReadOnlyList<string> All { get; } = [Read];
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
/// </remarks>
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
            .Where(s => s.Length > 0);

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
    /// <summary>The repository this credential is for, as that provider spells it.</summary>
    public required string Repo { get; init; }

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

    /// <summary>The repository it is for.</summary>
    public required string Repo { get; init; }

    public required CredentialReference Reference { get; init; }

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
