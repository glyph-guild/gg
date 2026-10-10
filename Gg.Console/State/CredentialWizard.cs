using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>Where the add-a-credential wizard is.</summary>
public enum CredentialWizardStep
{
    /// <summary>Which service the token is for.</summary>
    Service,

    /// <summary>What on that service: a repository, its work items, its analysis.</summary>
    For,

    /// <summary>How much access, in the service's own words.</summary>
    Access,

    /// <summary>Which account it acts as, and the token itself.</summary>
    Account,

    /// <summary>The whole of it, said back, before anything is registered.</summary>
    Review,
}

/// <summary>
/// The credential being added, as far as a person has answered.
/// </summary>
/// <param name="Step">Which step is showing.</param>
/// <param name="Cursor">The row under the cursor on a step that is a list.</param>
/// <param name="Service">The catalog key of the service, or null for something else.</param>
/// <param name="Subject">What it is for, once chosen.</param>
/// <param name="Named">Which one: a repository path, a tracker or service key.</param>
/// <param name="Scope">The widest scope chosen.</param>
/// <param name="Identity">The account it acts as.</param>
public sealed record CredentialDraft(
    CredentialWizardStep Step,
    int Cursor = 0,
    string? Service = null,
    string? Subject = null,
    string? Named = null,
    string? Scope = null,
    string? Identity = null);

/// <summary>One thing a credential can be for, on the service chosen.</summary>
public sealed record CredentialTarget(string Subject, string Named, string Said);

/// <summary>One access a credential can grant, in the service's words.</summary>
public sealed record CredentialAccess(string Scope, string Words);

/// <summary>What each step of the wizard offers.</summary>
public static class CredentialWizard
{
    /// <summary>The services a person can be sent to make a token for.</summary>
    public static IReadOnlyList<CredentialProvider> Services { get; } = [];

    /// <summary>What a credential on the chosen service can be for.</summary>
    public static IReadOnlyList<CredentialTarget> Targets(AppState state) => [];

    /// <summary>The accesses the chosen subject can be granted.</summary>
    public static IReadOnlyList<CredentialAccess> Accesses(AppState state) => [];
}
