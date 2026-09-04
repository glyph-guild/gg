namespace Gg.Runner.Vcs;

/// <summary>
/// One entry of <c>GG_VCS_HOSTS</c>, parsed once for everybody who reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two sides read that variable, and they used to read it differently.</b>
/// <see cref="VcsConfiguration.FromEnvironment"/> builds read adapters from it;
/// <see cref="DestinationConfiguration"/> reads the same variable to find the
/// git host a proposal's branch goes to. Each carried its own copy of the
/// suffix stripping, which worked for exactly as long as there was one suffix.
/// </para>
/// <para>
/// <b>A second one would not have failed where it was added.</b> Reading would
/// have kept working and every test about reading would have passed; the first
/// PUSH would have gone to a host with <c>!something</c> still inside it. That
/// is why this is one type rather than two well-intentioned copies.
/// </para>
/// <para>
/// <b>A suffix declares how a forge DIFFERS from the default convention, and
/// never names a forge.</b> Which forge a tenant uses stays the control plane's
/// knowledge; this binary is public and knows only shapes.
/// </para>
/// </remarks>
public readonly record struct HostDeclaration
{
    /// <summary>Suffix declaring that pull-request heads are NOT served from the base.</summary>
    public const string NoPullRequestHeads = "!nopr";

    /// <summary>
    /// Suffix declaring that this forge scopes repositories by path.
    /// </summary>
    /// <remarks>
    /// It selects a different pair of adapters on both sides — a clone url with
    /// no <c>.git</c> suffix, and a proposal spelled the way that forge spells
    /// one. The two differences belong to a single forge's spelling and travel
    /// together, so one declaration selects both rather than making a deployment
    /// state the same fact twice.
    /// </remarks>
    public const string PathScoped = "!pathscoped";

    /// <summary>Every suffix this parser recognises. Anything else is part of the host.</summary>
    private static readonly string[] _suffixes = [NoPullRequestHeads, PathScoped];

    /// <summary>The provider key, as configured.</summary>
    public required string Key { get; init; }

    /// <summary>The host, with every recognised suffix removed.</summary>
    public required string Host { get; init; }

    /// <summary>Whether this forge serves pull-request heads from the base repository.</summary>
    public required bool ServesPullRequestHeads { get; init; }

    /// <summary>Whether this forge scopes repositories by path.</summary>
    public required bool IsPathScoped { get; init; }

    /// <summary>
    /// Parses one <c>key=host</c> entry, or throws naming the entry.
    /// </summary>
    /// <remarks>
    /// Article XI. A malformed entry is not silently skipped: a runner that
    /// quietly serves fewer providers than somebody configured fails much later,
    /// on one flight, for a reason nothing connects back to a typo in a
    /// variable.
    /// </remarks>
    public static HostDeclaration Parse(string entry, string variable)
    {
        var parts = (entry ?? "").Split('=', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            throw new InvalidOperationException(
                $"{variable} entry '{entry}' is not key=host. Expected a comma-separated list "
              + $"like 'forge=forge.example.com', with '{NoPullRequestHeads}' appended to a host "
              + "that does not publish pull-request heads on the base repository, and "
              + $"'{PathScoped}' to one that scopes repositories by path.");
        }

        var host = parts[1];
        var servesPullRequestHeads = true;
        var pathScoped = false;

        // A LOOP, because two suffixes may be written in either order and
        // neither order is more correct to somebody typing a variable. Only a
        // RECOGNISED suffix is removed - trimming any trailing `!…` would
        // quietly rewrite a host nobody meant as a declaration.
        for (var matched = true; matched;)
        {
            matched = false;

            foreach (var suffix in _suffixes)
            {
                if (!host.EndsWith(suffix, StringComparison.Ordinal))
                {
                    continue;
                }

                host = host[..^suffix.Length];
                matched = true;

                if (suffix == NoPullRequestHeads)
                {
                    servesPullRequestHeads = false;
                }
                else
                {
                    pathScoped = true;
                }
            }
        }

        if (host.Length == 0)
        {
            throw new InvalidOperationException(
                $"{variable} entry '{entry}' declares suffixes and no host.");
        }

        return new HostDeclaration
        {
            Key = parts[0],
            Host = host,
            ServesPullRequestHeads = servesPullRequestHeads,
            IsPathScoped = pathScoped,
        };
    }

    /// <summary>Every entry in a comma-separated declaration.</summary>
    public static IEnumerable<HostDeclaration> ParseAll(string raw, string variable) =>
        (raw ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => Parse(entry, variable));

    /// <summary>
    /// Whether this declaration serves <paramref name="link"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The comparison the control plane cannot make.</b> A link is resolved
    /// to a registered repository by its PATH alone, so a link at any host
    /// resolves to whichever entry shares that path — contract 0.86.0 recorded
    /// that and left it, because the registry deliberately holds no host:
    /// <i>"a policy store that contained hosts would make credential destination
    /// a policy edit."</i> The mapping lives here instead, so the check does too.
    /// </para>
    /// <para>
    /// <b>The prefix is what scopes an organisation above the path.</b> A forge
    /// serving many tenants from one host is the case a bare host cannot tell
    /// apart, and it is the common one.
    /// </para>
    /// <para>
    /// <b>Case-insensitive on both halves, deliberately.</b> Hosts are, and the
    /// organisation segment is on every forge this has met. The outcome of this
    /// comparison is a REFUSAL, and a refusal that fires on a capital letter is
    /// one nobody can act on — too strict is the dangerous direction here.
    /// </para>
    /// </remarks>
    public bool Serves(Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var declared = Host.Split(
            '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (declared.Length == 0
            || !string.Equals(link.Host, declared[0], StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = link.AbsolutePath.Split(
            '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // The declaration's own segments must come FIRST and in order. Anything
        // after them is the forge's business - a project, a `_git`, a vanity
        // segment - which is the same latitude the registry's path matching
        // already allows.
        if (segments.Length < declared.Length - 1)
        {
            return false;
        }

        for (var at = 1; at < declared.Length; at++)
        {
            if (!string.Equals(segments[at - 1], declared[at], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Why <paramref name="uri"/> is not a link this runner serves for
    /// <paramref name="provider"/>, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only a DECLARED mismatch refuses.</b> A provider this runner declares
    /// nothing for is a capability gap the vcs adapter already reports in its own
    /// words; refusing it here would be a second, worse copy of that message, and
    /// would ground flights on a runner that had simply not been told about a
    /// forge.
    /// </para>
    /// <para>
    /// <b>A flight with no link is never refused for where it came from.</b> A
    /// ticket names a provider and an id; a sentence names nothing. Neither has a
    /// host to check, and inventing one for them would refuse work that is right.
    /// </para>
    /// </remarks>
    public static string? Unserved(
        string? provider, string? uri, IReadOnlyList<HostDeclaration> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        if (provider is not { Length: > 0 }
            || !Uri.TryCreate(uri, UriKind.Absolute, out var link)
            || link.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var serving = declared.FirstOrDefault(
            d => string.Equals(d.Key, provider, StringComparison.Ordinal));

        if (serving.Key is not { Length: > 0 } || serving.Serves(link))
        {
            return null;
        }

        return $"This flight's link is at '{link.Host}', and this runner serves '{provider}' at "
             + $"'{serving.Host}'. Nothing was fetched: a link that merely shares a path with a "
             + $"registered repository is not a link to it. Correct the flight's link, or declare "
             + $"'{link.Host}' in {VcsConfiguration.HostsVariable} if this runner should serve it.";
    }

    /// <summary>
    /// The provider key whose declaration serves this link, or null.
    /// </summary>
    /// <remarks>
    /// <b>What gives a link-shaped work item a tool.</b> A tracker reader is
    /// keyed on a provider and a link carries none, so without this a flight
    /// opened from a work-item URL reaches an agent with nothing that can read
    /// it. Two declarations serving one link answer NOTHING: that is a
    /// configuration question for a person, and picking one would be the
    /// inference slice nine retired.
    /// </remarks>
    public static string? ProviderFor(string? uri, IReadOnlyList<HostDeclaration> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var link)
            || link.Scheme is not ("http" or "https"))
        {
            return null;
        }

        string? found = null;
        foreach (var candidate in declared)
        {
            if (!candidate.Serves(link))
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = candidate.Key;
        }

        return found;
    }
}
