using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// A credential as a person reads it: which service, for what, granting what,
/// acting as whom.
/// </summary>
/// <param name="Service">The service's name, or null when gg cannot tell which one.</param>
/// <param name="For">What it is for: a repository, a tracker's place, an agent.</param>
/// <param name="Access">
/// What it grants, in the service's own words where gg knows them, and gg's own
/// scope words where it does not.
/// </param>
/// <param name="Identity">The account it acts as.</param>
/// <param name="Locator">Where it is kept, for the commands that still need to name it.</param>
public sealed record CredentialName(
    string? Service, string For, string Access, string Identity, string Locator)
{
    /// <summary>Whether gg recognised the service.</summary>
    public bool Known => Service is not null;

    /// <summary>The name in a list: <c>&lt;service&gt; · acme/payments</c>.</summary>
    public string Short => Known ? $"{Service} · {For}" : For;

    /// <summary>What it grants, in a cell: <c>Code (Read &amp; write), as kevin</c>.</summary>
    /// <remarks>
    /// <b>An unrecognised service is marked here too</b>, because this is the line a
    /// list and the console show, and one reading like every recognised line tells
    /// a person gg knows what it opens.
    /// </remarks>
    public string Grants => Known
        ? $"{Access}, as {Identity}"
        : $"{Access}, as {Identity} - service not recognised";

    /// <summary>
    /// The whole of it in one sentence, for the line a person reads before and
    /// after handing a token over.
    /// </summary>
    /// <remarks>
    /// <b>An unrecognised service is said, with the locator.</b> The sentence is
    /// what tells a person what they gave away; leaving the service out silently
    /// would read as gg knowing and not saying.
    /// </remarks>
    public string Sentence => Known
        ? $"Acts as {Identity} on {Service}, with {Access} on {For}."
        : $"Acts as {Identity}, with {Access} on {For}. gg cannot tell which service this "
        + $"is for; it is kept at {Locator}.";
}

/// <summary>
/// What this machine and this tenant say about where credentials go: each
/// registered repository's provider key, and each declared tracker's host.
/// </summary>
/// <param name="RepositoryProviders">Repository path to the provider key it was registered with.</param>
/// <param name="TrackerHosts">Tracker key to the host it is read from.</param>
public sealed record CredentialPlaces(
    IReadOnlyDictionary<string, string> RepositoryProviders,
    IReadOnlyDictionary<string, string> TrackerHosts)
{
    /// <summary>Nothing known, so every name is from the locator and a known key alone.</summary>
    public static CredentialPlaces None { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>From the repository registry and this machine's declared trackers.</summary>
    public static CredentialPlaces From(
        IEnumerable<RepositoryRegistered> repositories,
        IEnumerable<(string Key, string Host)> trackers)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(trackers);

        var providers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var repository in repositories)
        {
            providers.TryAdd(repository.Path, repository.Provider);
        }

        var hosts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, host) in trackers)
        {
            hosts.TryAdd(key, host);
        }

        return new CredentialPlaces(providers, hosts);
    }
}

/// <summary>
/// The one way a credential is named to a person, everywhere gg names one.
/// </summary>
/// <remarks>
/// <para>
/// <b>One function, so the list, the console, the doctor and the line after
/// registering cannot disagree.</b> Each of them used to print the locator,
/// which is a place and not a description - <c>local:tracker/ado</c> says where
/// a secret is kept and nothing about what it opens.
/// </para>
/// <para>
/// <b>Recognised from facts, never guessed.</b> A repository's service comes
/// from the provider key it was registered with; a tracker's from the host this
/// machine reads it at, then its key; an agent's from its name. Anything else
/// keeps gg's own words and says it could not tell.
/// </para>
/// </remarks>
public static class CredentialNames
{
    private const string TrackerPrefix = CredentialLocator.LocalPrefix + "tracker/";
    private const string AgentPrefix = CredentialLocator.LocalPrefix + "agent/";
    private const string AnalysisPrefix = CredentialLocator.LocalPrefix + "analysis/";

    public static CredentialName Describe(
        CredentialReference reference, string forName, CredentialPlaces places)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(places);

        var locator = reference.Locator;

        // A VAULT REFERENCE NAMES A SECRET, not a subject, so nothing here can
        // say what it is for beyond the name it was registered under.
        if (!locator.StartsWith(CredentialLocator.LocalPrefix, StringComparison.Ordinal))
        {
            return Unknown(reference, forName);
        }

        if (locator.StartsWith(TrackerPrefix, StringComparison.Ordinal))
        {
            var key = locator[TrackerPrefix.Length..];
            var host = places.TrackerHosts.TryGetValue(key, out var h) ? h : null;
            var provider = CredentialProviders.Find(host) ?? CredentialProviders.Find(key);

            return Named(provider, CredentialSubjects.Tracker, Place(host) ?? key, reference);
        }

        // A CODE-ANALYSIS SERVICE'S, by its key - which is the service's own name
        // for the services gg knows.
        if (locator.StartsWith(AnalysisPrefix, StringComparison.Ordinal))
        {
            var service = locator[AnalysisPrefix.Length..];

            return Named(
                CredentialProviders.Find(service), CredentialSubjects.Analysis, service, reference);
        }

        if (locator.StartsWith(AgentPrefix, StringComparison.Ordinal))
        {
            var agent = locator[AgentPrefix.Length..];

            return Named(
                CredentialProviders.Find(agent), CredentialSubjects.Agent, $"the {agent} agent",
                reference);
        }

        // A REPOSITORY'S, joined on the locator the same way the credentials
        // pane joins them, so a slug typed in another case still finds its row.
        var repository = places.RepositoryProviders
            .FirstOrDefault(r => string.Equals(
                CredentialLocator.ForRepo(r.Key), locator, StringComparison.Ordinal));

        return repository.Key is { } path
            ? Named(
                CredentialProviders.Find(repository.Value), CredentialSubjects.Repository, path,
                reference)
            : Named(null, CredentialSubjects.Repository, Nonempty(forName, locator), reference);

        static CredentialName Named(
            CredentialProvider? provider, string subject, string named, CredentialReference reference) =>
            new(
                provider?.Name,
                named,
                provider?.Access(subject, reference.Scopes) ?? string.Join(", ", reference.Scopes),
                reference.Identity,
                reference.Locator);
    }

    private static CredentialName Unknown(CredentialReference reference, string forName) =>
        new(
            null,
            Nonempty(forName, reference.Locator),
            string.Join(", ", reference.Scopes),
            reference.Identity,
            reference.Locator);

    /// <summary>
    /// The organisation and project in a tracker's host, which is what a person
    /// knows it by: <c>https://tracker.example/acme/board</c> is <c>acme/board</c>.
    /// </summary>
    private static string? Place(string? host) =>
        host is { Length: > 0 }
        && Uri.TryCreate(host, UriKind.Absolute, out var uri)
        && uri.AbsolutePath.Trim('/') is { Length: > 0 } path
            ? Uri.UnescapeDataString(path)
            : host;

    private static string Nonempty(string named, string fallback) =>
        string.IsNullOrWhiteSpace(named) ? fallback : named;
}
