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

/// <summary>What each step of the wizard offers, and what it says.</summary>
/// <remarks>
/// <para>
/// <b>Pure, so every step is a test rather than a walk.</b> The screen draws
/// what this returns; the reducer moves through it; neither decides what a
/// service offers.
/// </para>
/// <para>
/// <b>Named the way everything else names a credential</b>: the review is
/// <see cref="CredentialNames.Describe"/>'s sentence over the draft, so what a
/// person agrees to here is word for word what <c>gg credential list</c> will
/// say about it afterwards.
/// </para>
/// </remarks>
public static class CredentialWizard
{
    /// <summary>
    /// The services a person can be sent to make a token for: the catalog's,
    /// less those whose token gg mints itself.
    /// </summary>
    public static IReadOnlyList<CredentialProvider> Services { get; } =
        [.. CredentialProviders.All.Where(p => p.TokenPage is not null)];

    /// <summary>The last row on the first step, for a service gg does not know.</summary>
    public const string SomethingElse = "something else";

    /// <summary>The service the draft names, or null for something else or none yet.</summary>
    public static CredentialProvider? Service(CredentialDraft? draft) =>
        draft?.Service is { } key
            ? CredentialProviders.All.FirstOrDefault(p => p.Key == key)
            : null;

    /// <summary>What a credential on the chosen service can be for.</summary>
    /// <remarks>
    /// <b>A service's repositories are the ones registered with it</b>, by the
    /// provider key each was registered under - so a list for one service never
    /// offers another's. "Something else" offers every repository, because what
    /// it is for is all gg can say about a service it does not know.
    /// </remarks>
    public static IReadOnlyList<CredentialTarget> Targets(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var repositories = state.Repositories?.Repositories ?? [];
        var service = Service(state.CredentialDraft);

        if (service is null)
        {
            return [.. repositories.Select(r => new CredentialTarget(
                CredentialSubjects.Repository, r.Path, r.Path))];
        }

        var subjects = service.Accesses.Keys.Select(k => k.Subject).ToHashSet(StringComparer.Ordinal);
        var targets = new List<CredentialTarget>();

        if (subjects.Contains(CredentialSubjects.Repository))
        {
            targets.AddRange(repositories
                .Where(r => CredentialProviders.Find(r.Provider) == service)
                .Select(r => new CredentialTarget(
                    CredentialSubjects.Repository, r.Path, $"{r.Path}, its code")));
        }

        if (subjects.Contains(CredentialSubjects.Tracker))
        {
            // UNDER THE KEY THIS FLEET ALREADY USES for the service, which is the
            // one its declarations read - `ado`, not the catalog's own key.
            var key = service.Aliases.FirstOrDefault() ?? service.Key;
            targets.Add(new CredentialTarget(
                CredentialSubjects.Tracker, key, $"its work items, read as '{key}'"));
        }

        if (subjects.Contains(CredentialSubjects.Analysis))
        {
            targets.Add(new CredentialTarget(
                CredentialSubjects.Analysis, service.Key, "its findings about your code"));
        }

        return targets;
    }

    /// <summary>The accesses the chosen subject can be granted, narrowest first.</summary>
    public static IReadOnlyList<CredentialAccess> Accesses(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var draft = state.CredentialDraft;
        var service = Service(draft);

        if (service is null)
        {
            return
            [
                new(CredentialScopes.Read, "read"),
                new(CredentialScopes.Write, "read and write"),
            ];
        }

        var subject = draft?.Subject ?? "";

        return
        [
            .. ((string[])[CredentialScopes.Read, CredentialScopes.Write])
                .Where(scope => service.Accesses.ContainsKey((subject, scope)))
                .Select(scope => new CredentialAccess(scope, service.Accesses[(subject, scope)])),
        ];
    }

    /// <summary>The rows the step showing is a list of, or none on a step that is not one.</summary>
    public static IReadOnlyList<string> Rows(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.CredentialDraft?.Step switch
        {
            CredentialWizardStep.Service => [.. Services.Select(s => s.Name), SomethingElse],
            CredentialWizardStep.For => [.. Targets(state).Select(t => t.Said)],
            CredentialWizardStep.Access => [.. Accesses(state).Select(a => a.Words)],
            _ => [],
        };
    }

    /// <summary>The credential as the draft describes it, or null before it can be.</summary>
    public static CredentialName? Named(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.CredentialDraft is not
            { Subject: { } subject, Named: { } named, Scope: { } scope, Identity: { } identity })
        {
            return null;
        }

        return CredentialNames.Describe(
            new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = CredentialLocator.For(subject, named),
                Identity = identity,
                Scopes = scope == CredentialScopes.Write
                    ? [CredentialScopes.Read, CredentialScopes.Write]
                    : [CredentialScopes.Read],
            },
            named,
            CredentialPlaces.From(state.Repositories?.Repositories ?? [], []));
    }

    /// <summary>What the step showing asks, above its list or its fields.</summary>
    public static string Said(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var draft = state.CredentialDraft;
        var service = Service(draft);
        var called = service?.Name ?? "the service";

        return draft?.Step switch
        {
            CredentialWizardStep.Service =>
                "Which service is this token for? It is sent nowhere else.",

            CredentialWizardStep.For => Targets(state).Count == 0
                ? $"Nothing registered here is on {called}. Register the repository first, "
                + "or go back and choose another service."
                : $"What on {called} is it for?",

            CredentialWizardStep.Access => service?.TokenPage is { } page
                ? $"How much may it do? These are {called}'s own words: make the token at\n"
                + $"  {page}\nand tick the same there."
                : "How much may it do?",

            CredentialWizardStep.Account =>
                $"Which account does it act as - {service?.IdentityHint ?? "the account the token belongs to"}? "
              + "Then paste the token. It is not shown, and it is never part of gg's state.",

            CredentialWizardStep.Review => Named(state) is { } name
                ? $"{name.Sentence}\nThe token stays on this machine, sealed to you. "
                + "Enter registers it."
                : "Something is missing; go back a step.",

            _ => "",
        };
    }

    /// <summary>The short help beside each step.</summary>
    public static string Help(CredentialWizardStep step) => step switch
    {
        CredentialWizardStep.Service => "The services gg knows by name. Anything else is "
                                      + "named by what it is for.",
        CredentialWizardStep.For => "A repository's code, a tracker's work items, or an "
                                  + "analysis service's findings.",
        CredentialWizardStep.Access => "The narrowest that will do. A flight cannot use more "
                                     + "than the token allows.",
        CredentialWizardStep.Account => "The account is what a flight log names. The token "
                                      + "is held for this one registration and then sealed.",
        CredentialWizardStep.Review => "Nothing has been registered yet. Back changes an "
                                     + "answer; esc drops all of it.",
        _ => "",
    };
}
