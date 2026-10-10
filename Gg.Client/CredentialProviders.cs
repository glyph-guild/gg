using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// A service gg knows a credential can be for, in that service's own words.
/// </summary>
/// <param name="Key">How gg refers to it, everywhere: <c>azure-devops</c>, <c>sonarcloud</c>.</param>
/// <param name="Name">What a person calls it.</param>
/// <param name="Hosts">The hosts that are this service, matched exactly or as a parent domain.</param>
/// <param name="Aliases">Keys a person or a configuration already uses for it, such as <c>ado</c>.</param>
/// <param name="TokenPage">Where a person makes a token for it, or null where gg mints its own.</param>
/// <param name="IdentityHint">What the account a credential acts as means on this service.</param>
/// <param name="Accesses">
/// What each of gg's scopes grants, per subject, in the words the service's own
/// token page uses - so the sentence a person reads before saving a token is the
/// one they ticked on the page that made it.
/// </param>
public sealed record CredentialProvider(
    string Key,
    string Name,
    IReadOnlyList<string> Hosts,
    IReadOnlyList<string> Aliases,
    string? TokenPage,
    string IdentityHint,
    IReadOnlyDictionary<(string Subject, string Scope), string> Accesses)
{
    /// <summary>
    /// The service's words for these scopes on this subject, or null when gg has
    /// none - in which case the caller says the scopes and not a guess.
    /// </summary>
    public string? Access(string subject, IReadOnlyList<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        // THE WIDEST SCOPE NAMES THE ACCESS. Write includes read on every service
        // here, so "Code (Read & write)" already says the read; listing both would
        // read as two grants.
        var widest = scopes.Contains(CredentialScopes.Write, StringComparer.Ordinal)
            ? CredentialScopes.Write
            : scopes.Contains(CredentialScopes.Read, StringComparer.Ordinal)
                ? CredentialScopes.Read
                : null;

        return widest is not null && Accesses.TryGetValue((subject, widest), out var words)
            ? words
            : null;
    }
}

/// <summary>
/// The services gg knows by name, and how a host or a key finds one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a catalog at all.</b> Every place a credential reached a person printed
/// its locator - <c>local:tracker/ado</c>, <c>local:jdx/jdnext</c> - and nothing in
/// the binary knew that <c>ado</c> is Azure DevOps, or that <c>write</c> on a
/// repository there means pushing code. A person registering a token could not
/// tell from gg what they were handing over. This is the one place that knows.
/// </para>
/// <para>
/// <b>Recognised, never guessed.</b> A host or key this does not know finds
/// nothing, and the caller says so in words rather than naming the nearest
/// service. A wrong name on a credential is worse than the locator it replaced.
/// </para>
/// </remarks>
public static class CredentialProviders
{
    public static CredentialProvider AzureDevOps { get; } = new(
        Key: "azure-devops",
        Name: "Azure DevOps",
        Hosts: ["dev.azure.com", "visualstudio.com"],
        Aliases: ["ado", "azure-devops", "azdo"],
        TokenPage: "https://dev.azure.com/{organization}/_usersSettings/tokens",
        IdentityHint: "the Azure DevOps user the token belongs to",
        Accesses: new Dictionary<(string, string), string>
        {
            [(CredentialSubjects.Repository, CredentialScopes.Read)] = "Code (Read)",
            [(CredentialSubjects.Repository, CredentialScopes.Write)] = "Code (Read & write)",
            [(CredentialSubjects.Tracker, CredentialScopes.Read)] = "Work Items (Read)",
            [(CredentialSubjects.Tracker, CredentialScopes.Write)] = "Work Items (Read & write)",
        });

    public static CredentialProvider SonarCloud { get; } = new(
        Key: "sonarcloud",
        Name: "SonarCloud",
        Hosts: ["sonarcloud.io"],
        Aliases: ["sonarcloud", "sonar", "sonarqube-cloud"],
        TokenPage: "https://sonarcloud.io/account/security",
        IdentityHint: "the SonarCloud user the token belongs to",
        Accesses: new Dictionary<(string, string), string>
        {
            // NOT YET A SUBJECT THE CONTRACT KNOWS: a SonarCloud token is for an
            // analysis service, which is neither a repository nor a tracker. The
            // words are here so the day the subject is, nothing else has to move.
            [("analysis", CredentialScopes.Read)] = "Browse (read issues and measures)",
        });

    public static CredentialProvider GitHub { get; } = new(
        Key: "github",
        Name: "GitHub",
        Hosts: ["github.com"],
        Aliases: ["github", "gh"],
        TokenPage: "https://github.com/settings/personal-access-tokens",
        IdentityHint: "the GitHub user the token belongs to",
        Accesses: new Dictionary<(string, string), string>
        {
            [(CredentialSubjects.Repository, CredentialScopes.Read)] = "Contents (Read)",
            [(CredentialSubjects.Repository, CredentialScopes.Write)] =
                "Contents and Pull requests (Read & write)",
            [(CredentialSubjects.Tracker, CredentialScopes.Read)] = "Issues (Read)",
            [(CredentialSubjects.Tracker, CredentialScopes.Write)] = "Issues (Read & write)",
        });

    public static CredentialProvider Claude { get; } = new(
        Key: "claude",
        Name: "Claude",
        Hosts: ["claude.ai", "anthropic.com"],
        Aliases: ["claude"],

        // NO PAGE: an agent's token is minted on the runner by `gg agent login`,
        // never pasted in.
        TokenPage: null,
        IdentityHint: "the Claude account the agent signs in as",
        Accesses: new Dictionary<(string, string), string>
        {
            [(CredentialSubjects.Agent, CredentialScopes.Read)] = "runs the agent",
            [(CredentialSubjects.Agent, CredentialScopes.Write)] = "runs the agent",
        });

    /// <summary>Every service gg knows, in the order a person is offered them.</summary>
    public static IReadOnlyList<CredentialProvider> All { get; } =
        [AzureDevOps, SonarCloud, GitHub, Claude];

    /// <summary>
    /// The service a host, URL or key names, or null.
    /// </summary>
    /// <remarks>
    /// <b>A host first, then a key.</b> A configured host is a fact about where the
    /// bytes go; a key is a name somebody chose, and only a known alias of one is
    /// taken as a service.
    /// </remarks>
    public static CredentialProvider? Find(string? hostOrKey)
    {
        if (hostOrKey is not { Length: > 0 } given)
        {
            return null;
        }

        if (HostOf(given) is { } host
            && All.FirstOrDefault(p => p.Hosts.Any(h => Is(host, h))) is { } byHost)
        {
            return byHost;
        }

        return All.FirstOrDefault(
            p => p.Aliases.Contains(given.Trim().ToLowerInvariant(), StringComparer.Ordinal));
    }

    /// <summary>The host in a URL or a bare <c>host/path</c>, or null for a plain key.</summary>
    private static string? HostOf(string value)
    {
        var text = value.Trim();

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
        {
            return uri.Host.ToLowerInvariant();
        }

        // `dev.azure.com/HRTMS`, the shape a forge declaration carries: a dot
        // before any slash is a host, and a key never has one.
        var host = text.Split('/')[0];

        return host.Contains('.', StringComparison.Ordinal) ? host.ToLowerInvariant() : null;
    }

    private static bool Is(string host, string known) =>
        string.Equals(host, known, StringComparison.Ordinal)
        || host.EndsWith("." + known, StringComparison.Ordinal);
}
