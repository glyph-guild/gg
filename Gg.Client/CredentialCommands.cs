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
    /// <summary>
    /// What a terminal's bracketed paste leaves behind, removed.
    /// </summary>
    /// <remarks>
    /// <b>The ESC goes and the rest stays, which is the bug.</b> A terminal
    /// wraps a paste in <c>ESC[200~</c> and <c>ESC[201~</c>; the reader below
    /// drops the two ESCs as control characters and keeps <c>[200~</c> and
    /// <c>[201~</c> in the secret. An agent login code mangled that way is
    /// refused by the agent, and all the refusal can say is that no token was
    /// printed - which is three rounds of a person pasting the same code.
    /// <para>
    /// <b>Only the exact markers, and only where they sit.</b> A secret is
    /// somebody else's bytes: cutting five characters off anything that starts
    /// with a bracket would be this side corrupting what it was handed.
    /// </para>
    /// </remarks>
    public static string Pasted(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);

        var cleaned = typed;
        if (cleaned.StartsWith("[200~", StringComparison.Ordinal))
        {
            cleaned = cleaned[5..];
        }

        if (cleaned.EndsWith("[201~", StringComparison.Ordinal))
        {
            cleaned = cleaned[..^5];
        }

        return cleaned;
    }

    /// <summary>What to say about a secret that was read but never shown.</summary>
    /// <remarks>
    /// <b>A count is not an echo.</b> Nothing about the value crosses the
    /// screen; how much of it arrived is what tells a person their paste landed,
    /// and nothing else in this prompt can.
    /// </remarks>
    public static string Received(int characters) =>
        characters == 0
            ? "(nothing was pasted or typed)"
            : $"({characters} characters received)";

    public string ReadSecret(string prompt)
    {
        System.Console.Write(prompt);

        if (System.Console.IsInputRedirected)
        {
            // Not a terminal. There is nothing to echo and nothing to hide, and
            // refusing here would break the one honest scripted case: piping a
            // secret in on stdin, which never touches argv or the environment.
            var piped = Pasted(System.Console.ReadLine() ?? "");
            System.Console.WriteLine();
            return piped;
        }

        var typed = new System.Text.StringBuilder();
        while (true)
        {
            var key = System.Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                var read = Pasted(typed.ToString());
                System.Console.WriteLine(Received(read.Length));
                return read;
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
    ISecretPrompt prompt,
    string? personKeyPath = null)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;
    private readonly ICredentialStore _credentials = credentials;
    private readonly ISecretPrompt _prompt = prompt;

    /// <summary>
    /// Where this person's key lives, when the default is not wanted.
    /// </summary>
    /// <remarks>
    /// <b>A TEST THAT READS THE REAL USER'S KEY IS A TEST THAT PASSES HERE AND
    /// FAILS IN CI</b>, which is exactly what happened the first time
    /// <c>Register</c> was wired: the add test went green on this machine because
    /// the developer running it had a key, and would have gone red on a runner that
    /// does not. It is the same hazard <c>FileCredentialStore</c>'s lazy key already
    /// names — a test that passed a temporary root and would then have written a key
    /// into the real user's configuration directory.
    /// </remarks>
    private readonly string? _personKeyPath = personKeyPath;

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
    /// <param name="named">
    /// What the credential is for, as the person naming it spelled it — a repository
    /// slug, an agent's key, a tracker's key.
    /// </param>
    /// <param name="subject">
    /// Which kind of thing <paramref name="named"/> names. One of
    /// <see cref="CredentialSubjects.All"/>; it defaults to a repository because that
    /// is the common case and every caller written before slice sixty-four step 4 meant
    /// one, but an unknown value is refused rather than narrowed to it.
    /// </param>
    public async Task<VerbResult> AddAsync(
        string named,
        IReadOnlyList<string> scopes,
        string? identity = null,
        string subject = CredentialSubjects.Repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(named);
        ArgumentNullException.ThrowIfNull(scopes);

        // THE SUBJECT IS CHECKED BEFORE ANYTHING ELSE, because a subject gg does not
        // know cannot produce a locator and narrowing it to a repository is exactly
        // how this fleet's tracker credential got a repository's locator.
        if (CredentialSubjects.Refuse(subject) is { } unknown)
        {
            throw new CredentialRefusedException(unknown);
        }

        var token = Session();

        // THE KEY IS FOUND BEFORE ANYTHING IS ASKED FOR. A credential is sealed to
        // the person who registers it (ADR-0037 Decision 2), so with no key there is
        // nothing to seal it to and the whole verb is going to refuse. Asking the
        // identity question first - let alone the secret - means a person answers
        // questions whose answers are thrown away, and the natural next move after
        // typing a token into a process that then refused is to type it again.
        var holder = APersonsPublicHalf();

        var wider = scopes.Where(s => !CredentialScopes.All.Contains(s)).ToList();
        if (wider.Count > 0)
        {
            throw new CredentialScopeException(
                $"Scope '{wider[0]}' is not one gg can ask for. This slice reads, and only reads: "
              + string.Join(", ", CredentialScopes.All) + ".");
        }

        // DERIVED IN THE ONE PLACE THAT KNOWS HOW. A switch here would be the second
        // derivation, and a subject added later would need finding in both.
        var locator = CredentialLocator.For(subject, named);

        var who = identity is { Length: > 0 }
            ? identity
            : _prompt.ReadLine($"Which account does this credential act as, on {named}? ");
        if (string.IsNullOrWhiteSpace(who))
        {
            throw new CredentialRefusedException(
                "A credential names the account it acts as. Without it a flight log cannot say "
              + "who read what it opened.");
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

        // SEALED TO THE PERSON WHO REGISTERED IT, not to this machine (ADR-0037
        // Decision 2, slice sixty-four step 2). The public half is all sealing
        // needs, so this costs no passphrase - what costs one is MOVING the
        // credential, which is the act that needs a human.
        //
        // The one place a secret enters this process. It goes to the store and
        // nowhere else; nothing below this line reads it again.
        _credentials.Register(
            locator, _prompt.ReadSecret($"Secret for {named} (not echoed): "), holder);

        try
        {
            var registered = await _client.RegisterCredentialAsync(
                token,
                new CredentialRegistrationRequest { For = named, Reference = reference },
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

    /// <summary>
    /// What a person adding a tracker's credential needs told about this machine.
    /// </summary>
    public static string? TrackerNotice(string tracker, IReadOnlyList<DeclaredTracker> declared) =>
        null;

    /// <summary>
    /// Every credential reference this tenant has registered, and how each one
    /// rests on this machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both in one answer</b>, for <c>VerbResult.AirspaceRepositories</c>'
    /// reason: they are one reading of the same credentials at the same moment,
    /// and two reads a caller had to remember to pair are two that eventually
    /// disagree about how many rows there are.
    /// </para>
    /// <para>
    /// <b>Nothing here opens one.</b> `gg doctor`'s credentials row answers a
    /// near-identical question with <c>Read</c>, which decrypts every credential
    /// on this machine and reseals the plaintext ones on the way past. A list is
    /// read far more often than a doctor, and one that decrypted would pull every
    /// secret on the machine into a process whose job is to print four columns.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> ListCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var registered = await _client.ListCredentialsAsync(Session(), cancellationToken);

        return new VerbResult.Credentials(
            registered,
            CredentialsAtRest.For(
                registered.Credentials, _credentials.RestingOf, _credentials.HoldersOf));
    }

    /// <summary>
    /// Mints this person's key, wraps it with a passphrase, and registers its
    /// public half.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>HERE RATHER THAN IN THE CLI'S COMPOSITION ROOT, which is where it was.</b>
    /// `gg key create` owned this inline, so the console could not reach it: a pane
    /// may only load through a verb, and there was no verb. Two surfaces sharing one
    /// implementation is the rule this file states — <i>"different renderers over
    /// one result type, never a second way to get the data"</i> — and minting was
    /// the one credential act with only one way in.
    /// </para>
    /// <para>
    /// <b>The passphrase is read here and kept nowhere.</b> It is typed twice,
    /// because gg keeps no copy of it and a mistyped one means re-minting every
    /// credential it protected. Neither the value nor the private half is returned,
    /// held in a field, or reachable from the result.
    /// </para>
    /// <para>
    /// <b>Registering is part of the act, not a follow-up.</b> A key nobody can look
    /// up is a key nobody can seal to, so minting without registering leaves
    /// somebody holding something no colleague can address — and that failure
    /// arrives much later as "I cannot find your key" rather than here, where it can
    /// be fixed. A registration that does not land is SAID and does not fail the
    /// act: the key is written either way, and refusing would leave a file on disk
    /// the person was told nothing about.
    /// </para>
    /// </remarks>
    /// <param name="keyPath">
    /// Where to write it, or null for this machine's usual place. A seam for tests,
    /// so asserting that NO key was written does not mean writing one over the
    /// developer's own.
    /// </param>
    public async Task<VerbResult> CreateKeyAsync(
        CancellationToken cancellationToken = default, string? keyPath = null)
    {
        // THE SESSION FIRST, WHICH IS AddAsync's RULE AND THIS VERB BROKE IT.
        // "The session is checked FIRST, before the prompt, because asking somebody
        // for a token and then telling them to log in has taken a secret into a
        // process for nothing."
        //
        // AND MINTING DID WORSE THAN WASTE A PROMPT. It wrote the key before
        // finding out, and an unregistered key is a dead end: this verb refuses to
        // overwrite one and nothing else registers a public half, so the only way
        // forward was deleting it - the very act that refusal exists to prevent.
        // Checked here rather than inside the registration, which runs after the
        // key is on disk and cannot unwrite it.
        _ = Session();

        var passphrase = _prompt.ReadSecret("Passphrase for this key (not echoed): ");

        if (string.IsNullOrEmpty(passphrase))
        {
            throw new CredentialRefusedException(
                "A key is wrapped by a passphrase, and this one is empty. Nothing was written.");
        }

        if (!string.Equals(passphrase, _prompt.ReadSecret("Again: "), StringComparison.Ordinal))
        {
            throw new CredentialRefusedException(
                "Those did not match, so nothing was written. gg keeps no copy of this "
              + "passphrase, which is why it asks twice.");
        }

        var key = PersonKey.Create(path: keyPath, passphrase: passphrase);

        return new VerbResult.KeyCreated(new VerbResult.KeyMinted(
            At: keyPath ?? PersonKey.DefaultPath(),
            PublicKey: key.PublicKey,
            Registration: await RegisteredAsync(key.PublicKey, cancellationToken)));
    }

    /// <summary>
    /// Registers the public half, and says what happened either way.
    /// </summary>
    /// <remarks>
    /// <b>It never throws.</b> The key is already on disk by the time this runs, so
    /// a failure here is a sentence about what is left to do rather than a reason to
    /// pretend nothing happened.
    /// </remarks>
    private async Task<string> RegisteredAsync(
        string publicKey, CancellationToken cancellationToken)
    {
        if (_sessions.Read() is not { } session)
        {
            return "It is not registered yet - this machine is not signed in. Run `gg login` and "
                 + "then `gg key create` on a machine that is, or register this one later; until "
                 + "then nobody can look your key up to seal a credential to it.";
        }

        try
        {
            var registered = await _client.RegisterKeyAsync(
                session.SessionToken,
                new PrincipalKeyRegistrationRequest { PublicKey = publicKey },
                cancellationToken);

            return $"Registered as {registered.Fingerprint}, so a credential can be sealed to you.";
        }
        catch (Exception failure) when (failure is HttpRequestException or InvalidOperationException)
        {
            return $"Your key is written, and registering it did not work: {failure.Message} "
                 + "Nobody can seal a credential to you until it is registered.";
        }
    }

    /// <summary>
    /// Every public key this tenant's people have registered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Here rather than beside `gg key create`</b>, which lives in the CLI's
    /// own composition because it writes a file this assembly does not own. A
    /// READ of the tenant's keys is the same shape as a read of its credentials —
    /// one session, one GET, no local state — and the two questions are asked
    /// together: whose key can this credential be sealed to.
    /// </para>
    /// <para>
    /// <b>Retired keys come back too.</b> That is the control plane's choice and
    /// this does not filter it: a credential sealed to a key last year is still
    /// sealed to it, so "who could open this" is a question a retired key answers.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The public half of this person's key, or a refusal naming the one command
    /// that makes one.
    /// </summary>
    /// <remarks>
    /// <b>Asked BEFORE the secret is prompted for.</b> A person with no key who was
    /// asked for a token first would type a real credential into a process that then
    /// refused — and the natural next move is to type it again, which is one more
    /// place it has been.
    /// </remarks>
    private string APersonsPublicHalf()
    {
        try
        {
            return PersonKey.PublicHalfOf(_personKeyPath);
        }
        catch (CredentialUnavailableException absent)
        {
            throw new CredentialRefusedException(
                absent.Message
              + " A credential is sealed to the person who registers it, so there has to be a key "
              + "to seal it to before there can be a credential.");
        }
    }

    public async Task<VerbResult> ListKeysAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Keys(await _client.ListKeysAsync(Session(), cancellationToken));

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
    /// <summary>
    /// Makes this machine a holder of a credential sealed to the person running it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one act in this step that costs a passphrase</b>, because it unwraps:
    /// the content key comes out under the person's key and goes back in wrapped for
    /// this machine. Registering needs only a public half and asks for nothing
    /// (ADR-0037 Decision 2), so this is where a prompt means something.
    /// </para>
    /// <para>
    /// <b>Nothing is sent anywhere.</b> No session, no control-plane call, no
    /// network: who can open a credential on this disk is not a fact the control
    /// plane holds or may hold. That is also why the result is a local record rather
    /// than a contract type.
    /// </para>
    /// <para>
    /// <b>Not async in anything it does</b>, and kept async anyway so the verb
    /// dispatches like every other one. A method shaped differently from its
    /// neighbours is one somebody wires differently.
    /// </para>
    /// </remarks>
    public Task<VerbResult> TrustThisMachineAsync(
        string locator, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var before = _credentials.HoldersOf(locator).Count;

        var person = PersonKey.Unlock(
            _personKeyPath,
            passphrase: _prompt.ReadSecret(
                "Passphrase for your key, so this machine can be given a copy (not echoed): "));

        _credentials.TrustThisMachine(locator, person);

        var after = _credentials.HoldersOf(locator);

        return Task.FromResult<VerbResult>(new VerbResult.CredentialTrusted(
            new VerbResult.MachineTrusted(locator, after.Count, AlreadyWas: after.Count == before)));
    }

    /// <summary>
    /// Who the tenant has declared needs this credential, and who has reported they cannot
    /// resolve it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONE DERIVATION FOR BOTH DOORS.</b> The CLI's broadcast and the console's review
    /// ask the same question, and two places asking it is the hazard this codebase keeps
    /// naming — <i>"two derivations that agree today is how a runner ends up looking for a
    /// file the CLI never wrote."</i>
    /// </para>
    /// <para>
    /// <b>Two reads, each made once.</b> The fleet read replays every profile's event
    /// stream, because a profile is the newest entry on a stream rather than a row — so
    /// asking per recipient would replay them per machine.
    /// </para>
    /// <para>
    /// <b>A verb, because the console may only load through one.</b> A bare DTO reaching
    /// <c>ConsoleData</c> is what <c>Every_load_the_console_can_do_is_a_verb</c> refuses.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> AudienceAsync(
        string locator, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(locator);

        var token = Session();

        var runners = await _client.ListRunnersAsync(token, cancellationToken);
        var profiles = await _client.ListFleetProfilesAsync(token, cancellationToken);

        return new VerbResult.CredentialAudienceFound(new VerbResult.AudienceList(
            For: locator,
            Machines: CredentialAudience.For(locator, runners.Runners, profiles.Profiles)));
    }

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
