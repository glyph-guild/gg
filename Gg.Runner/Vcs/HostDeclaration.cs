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
}
