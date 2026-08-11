using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// How a secret gets into the process, and the only way it does.
/// </summary>
/// <remarks>
/// <para>
/// A port rather than a call to <c>Console.ReadKey</c>, so a test can answer
/// it - and, more to the point, so there is a single named place where a
/// secret enters. It is a short list to audit.
/// </para>
/// <para>
/// <b>Prompted, never an argument.</b> A flag would put the value in shell
/// history and in <c>ps</c> output before any code of ours ran, and neither of
/// those is somewhere a later fix can reach. There is no flag, and
/// <c>CredentialArgsTests</c> fails the build if one appears.
/// </para>
/// </remarks>
public interface ISecretPrompt
{
    /// <summary>Reads a secret. Nothing is echoed and nothing is kept.</summary>
    string ReadSecret(string prompt);

    /// <summary>Reads a fact - an account name - which is echoed, because it is not a secret.</summary>
    string ReadLine(string prompt);
}

/// <summary>Reads from the terminal, with the echo off for the secret.</summary>
public sealed class ConsoleSecretPrompt : ISecretPrompt
{
    public string ReadLine(string prompt)
    {
        System.Console.Write(prompt);
        return (System.Console.ReadLine() ?? "").Trim();
    }

    /// <summary>
    /// Reads without echoing, and without a backspace history.
    /// </summary>
    /// <remarks>
    /// Character by character rather than <c>ReadLine</c> with the echo
    /// disabled: redirected input has no console to disable, and a paste into
    /// a terminal that echoed the token would put it on the screen behind
    /// whoever is watching.
    /// </remarks>
    public string ReadSecret(string prompt)
    {
        System.Console.Write(prompt);

        if (System.Console.IsInputRedirected)
        {
            // Not a terminal. There is nothing to echo and nothing to hide, and
            // refusing here would break the one honest scripted case: piping a
            // secret in on stdin, which never touches argv or the environment.
            var piped = System.Console.ReadLine() ?? "";
            System.Console.WriteLine();
            return piped;
        }

        var typed = new System.Text.StringBuilder();
        while (true)
        {
            var key = System.Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                System.Console.WriteLine();
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }
    }
}

/// <summary>A scope this protocol does not grant.</summary>
public sealed class CredentialScopeException(string message) : Exception(message);

/// <summary>The control plane refused the reference, with a reason.</summary>
public sealed class CredentialRefusedException(string message) : Exception(message);

/// <summary>A credential id naming nothing this tenant has.</summary>
public sealed class CredentialNotFoundException(string message) : Exception(message);

/// <summary>
/// The credential verbs, run in the credential-broker role.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the smallest honest version of the product's claim at the heart.</b>
/// A developer registers a credential; the control plane stores a reference;
/// the runner resolves the secret locally. The secret never crosses.
/// </para>
/// <para>
/// It never crosses because there is nowhere for it to go: the registration
/// request type has no field capable of carrying secret material, which is
/// asserted over its shape rather than intended, and the only thing that ever
/// holds the value in this file is a local variable handed straight to the
/// store.
/// </para>
/// <para>
/// Every method returns a <see cref="VerbResult"/> and none of them writes
/// anything, the same as the flight verbs - which is what makes the console
/// and <c>--json</c> two renderings of one result.
/// </para>
/// </remarks>
public sealed class CredentialCommands(
    ControlPlaneClient client,
    ISessionStore sessions,
    ICredentialStore credentials,
    ISecretPrompt prompt)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;
    private readonly ICredentialStore _credentials = credentials;
    private readonly ISecretPrompt _prompt = prompt;

    /// <summary>
    /// Prompts for the secret, stores it locally, and registers a reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is deliberate and it is the order of the failures. The session
    /// is checked FIRST, before the prompt, because asking somebody for a token
    /// and then telling them to log in has taken a secret into a process for
    /// nothing. The scopes are checked next, by the contract's own rule, so a
    /// request the control plane would refuse is not sent - and no secret is
    /// read for it either.
    /// </para>
    /// <para>
    /// The secret is written before the reference is registered, because a
    /// reference pointing at a secret that is not there is a flight that
    /// stalls. If the registration is then refused, the local secret is removed
    /// again: an orphan file is nobody's friend, and this one would sit on disk
    /// with nothing pointing at it.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> AddAsync(
        string repo,
        IReadOnlyList<string> scopes,
        string? identity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentNullException.ThrowIfNull(scopes);

        var token = Session();

        var wider = scopes.Where(s => !CredentialScopes.All.Contains(s)).ToList();
        if (wider.Count > 0)
        {
            throw new CredentialScopeException(
                $"Scope '{wider[0]}' is not one gg can ask for. This slice reads, and only reads: "
              + string.Join(", ", CredentialScopes.All) + ".");
        }

        var locator = CredentialLocator.ForRepo(repo);

        var who = identity is { Length: > 0 }
            ? identity
            : _prompt.ReadLine($"Which account does this credential act as, on {repo}? ");
        if (string.IsNullOrWhiteSpace(who))
        {
            throw new CredentialRefusedException(
                "A credential names the account it acts as. Without it a flight log cannot say "
              + "who read the repository.");
        }

        var reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = locator,
            Identity = who.Trim(),
            Scopes = scopes,
        };

        if (CredentialReference.Validate(reference) is { } diagnosis)
        {
            throw new CredentialRefusedException(diagnosis);
        }

        // The one place a secret enters this process. It goes to the store and
        // nowhere else; nothing below this line reads it again.
        _credentials.Write(locator, _prompt.ReadSecret($"Secret for {repo} (not echoed): "));

        try
        {
            var registered = await _client.RegisterCredentialAsync(
                token,
                new CredentialRegistrationRequest { Repo = repo, Reference = reference },
                cancellationToken);

            return new VerbResult.CredentialAdded(registered);
        }
        catch (Exception)
        {
            // Refused, or the network went away. Either way the reference does
            // not exist, so neither should the secret it would have pointed at.
            _credentials.Remove(locator);
            throw;
        }
    }

    /// <summary>Every credential reference this tenant has registered.</summary>
    public async Task<VerbResult> ListCredentialsAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Credentials(await _client.ListCredentialsAsync(Session(), cancellationToken));

    /// <summary>
    /// Deregisters a credential, then deletes the local secret it named.
    /// </summary>
    /// <remarks>
    /// The order is the point, and it is the same order <c>gg logout</c> uses.
    /// Deleting locally first and then failing to deregister leaves the control
    /// plane pointing at a secret that is not there - which is a flight that
    /// stalls for a reason nobody can see. The other way round leaves an unused
    /// file, which is visible, harmless and removable.
    /// </remarks>
    public async Task<VerbResult> RemoveCredentialAsync(
        string credentialId, CancellationToken cancellationToken = default)
    {
        var token = Session();

        var removed = await _client.RemoveCredentialAsync(token, credentialId, cancellationToken)
            ?? throw new CredentialNotFoundException(
                $"No credential {credentialId}. Run gg credential list to see what is there.");

        _credentials.Remove(removed.Reference.Locator);

        return new VerbResult.CredentialRemoved(removed);
    }

    private string Session() =>
        _sessions.Read()?.SessionToken
        ?? throw new NotSignedInException("Not signed in. Run gg login.");
}
