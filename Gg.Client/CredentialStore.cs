using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// Where the secret actually lives, on this machine.
/// </summary>
/// <remarks>
/// A port with exactly one adapter. Keychain and Key Vault are adapter two and
/// adapter three, and neither ships in this slice - three platform
/// implementations is real work protecting against a threat this slice does
/// not address.
/// </remarks>
public interface ICredentialStore
{
    /// <summary>The directory everything here lives under.</summary>
    string Root { get; }

    /// <summary>What this store is and how it protects what it holds, in one sentence.</summary>
    /// <remarks>
    /// Printed by <c>gg doctor</c> verbatim. It must not imply protection the
    /// store does not have: this is a file with restrictive permissions, not
    /// encryption at rest, and saying otherwise is the one lie this slice
    /// cannot afford.
    /// </remarks>
    string Protection { get; }

    /// <summary>
    /// How THIS credential rests here, in one sentence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because one sentence for a directory stopped being true of everything
    /// in it.</b> A machine mid-migration holds sealed credentials and plaintext
    /// ones at once, and <see cref="Protection"/> is printed verbatim — so
    /// either it answers per credential or the migration is not allowed to be
    /// partial. The owner took the first on 2026-10-04: an atomic migration
    /// turns a locked file or a full disk into a machine that will not run, and
    /// a cosmetic honesty problem must not become a fleet outage.
    /// </para>
    /// <para>
    /// <b>It opens nothing</b>, for <see cref="Holds"/>'s reason. How a
    /// credential rests is the file's shape, and a sentence about a credential
    /// must never be a reason to decrypt one.
    /// </para>
    /// </remarks>
    string ProtectionFor(string locator);

    /// <summary>
    /// How THIS credential rests here, in one word from
    /// <see cref="CredentialResting"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same answer as <see cref="ProtectionFor"/>, short enough for a
    /// column.</b> That one is a sentence — the sealed one runs to about 180
    /// characters — which is right where it is printed once and impossible
    /// printed once per credential. `gg credential list` needs a column and so
    /// does the pane after it.
    /// </para>
    /// <para>
    /// <b>Here rather than derived by a caller, and that is the point.</b> A
    /// reader working the word out of the sentence — looking for "sealed" inside
    /// it — passes today and starts lying about whether a secret is encrypted the
    /// first time somebody rewords the sentence. Both answers come out of one
    /// decision in each implementation, so they cannot disagree.
    /// </para>
    /// <para>
    /// <b>It opens nothing</b>, for <see cref="Holds"/>'s reason, and it throws
    /// for a locator this store cannot place — the same contract
    /// <see cref="PathFor"/> has, because it is the same question about the same
    /// path.
    /// </para>
    /// </remarks>
    string RestingOf(string locator);

    /// <summary>
    /// Whose keys the credential at this locator is sealed to, or empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Public keys, and never the envelope.</b> Handing back a
    /// <see cref="SealedCredential"/> would put ciphertext in reach of everything
    /// that asks — including a console model that is written to disk — and a
    /// holder is a public key, which the contract says a refusal may name because
    /// saying so gives nothing away. This is the narrow half of that type, and the
    /// only half anything outside the store needs.
    /// </para>
    /// <para>
    /// <b>Empty is a real answer, three times over:</b> nothing is stored here,
    /// what is stored is plaintext and therefore sealed to nobody, or the locator
    /// lives in a vault this machine only reads. <see cref="RestingOf"/> already
    /// says which, so this does not need to.
    /// </para>
    /// <para>
    /// <b>It opens nothing.</b> Reading an envelope to see whose keys it names is
    /// not decrypting it, and the ciphertext is not touched — which matters because
    /// a pane asks this on every refresh.
    /// </para>
    /// </remarks>
    IReadOnlyList<string> HoldersOf(string locator);

    /// <summary>Where a locator's secret is kept. Throws if the locator is not one.</summary>
    string PathFor(string locator);

    /// <summary>Stores a secret against a locator, replacing whatever was there.</summary>
    void Write(string locator, string secret);

    /// <summary>
    /// A person registers a credential, sealed to that person and to nobody else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A DIFFERENT ACT FROM <see cref="Write"/>, not a flag on it</b>, and the
    /// reason is a call graph rather than taste. <c>Write</c> is reached by the
    /// pre-sealing plaintext migration on every read, and by a runner minting its
    /// own agent token — both of which must seal to the MACHINE, because neither
    /// has a person anywhere near it. One method that guessed which it was would
    /// guess wrong on a pool member and brick a credential silently.
    /// </para>
    /// <para>
    /// <b>It takes a public key rather than a key</b> (ADR-0037 Decision 2), so
    /// registering costs no passphrase: sealing TO somebody needs only their public
    /// half. What costs a passphrase is <see cref="TrustThisMachine"/>, which
    /// unwraps. A prompt here would derive nothing, and a prompt that protects
    /// nothing teaches that typing one is what makes a credential safe.
    /// </para>
    /// </remarks>
    /// <param name="holder">
    /// The person's public half, SubjectPublicKeyInfo in base64 — what
    /// <c>PersonKey.PublicHalfOf</c> returns.
    /// </param>
    void Register(string locator, string secret, string holder);

    /// <summary>
    /// Makes this machine a holder of a credential, on the authority of somebody
    /// who already is one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one act in the step that genuinely costs a passphrase</b>, because it
    /// is a rewrap: the content key is unwrapped under the person's key and wrapped
    /// again for this machine. It is Decision 8's second holder pointed at a
    /// machine, and it ADDS — the person who registered it stays a holder, or they
    /// could never push it anywhere else.
    /// </para>
    /// <para>
    /// <b>Asking twice leaves it trusted rather than broken.</b> <c>Rewrap</c>
    /// refuses a holder it already has, which is right for a caller deciding
    /// something and wrong to surface to somebody who asked for a state that
    /// already obtains.
    /// </para>
    /// </remarks>
    void TrustThisMachine(string locator, IAgreeAsAHolder person);

    /// <summary>
    /// Stores an envelope exactly as it arrived, without opening it.
    /// </summary>
    /// <remarks>
    /// <b>On the PORT rather than reached by a downcast.</b> A keeper that
    /// tested for the file store and did nothing for anything else would be a
    /// push that silently failed against whichever adapter arrives next -
    /// which is the shape of defect this codebase keeps finding, and a
    /// `written: false` nobody could explain.
    /// </remarks>
    void WriteSealed(string locator, SealedCredential envelope);

    /// <summary>The secret, or null when this machine does not have it.</summary>
    string? Read(string locator);

    /// <summary>
    /// Whether this machine has a secret for the locator, without reading one.
    /// </summary>
    /// <remarks>
    /// <b>A separate verb because <see cref="Read"/> hands back the secret.</b>
    /// Whether a credential is present is a fact a screen may show and a state
    /// dump may carry; the secret is neither. A console that answered this by
    /// calling <see cref="Read"/> and testing for null would have pulled every
    /// credential this machine holds into the process that draws the screen -
    /// and the console may not resolve a credential at all.
    /// </remarks>
    bool Holds(string locator);

    /// <summary>Deletes it. False when there was nothing to delete.</summary>
    bool Remove(string locator);
}

/// <summary>
/// A mode-0600 file, in the platform config directory, beside the session.
/// </summary>
/// <remarks>
/// <para>
/// <b>One store, not two.</b> Step 2b put the session token in a 0600 file and
/// left the keychain question open for this step. The answer is that a second
/// mechanism for a second kind of secret is a component nobody asked for -
/// Article X, prefer fewer components - so the credential lives beside the
/// session, under the same rules, in the same directory somebody has to find
/// exactly once.
/// </para>
/// <para>
/// <b>Be honest about what this protects.</b> The security property this slice
/// delivers is that the secret never reaches the control plane. It is not
/// at-rest encryption on a developer's laptop: anything running as this uid
/// can read the file, and <c>gg doctor</c> says so in those words rather than
/// implying keychain-grade protection.
/// </para>
/// <para>
/// The locator is validated by the CONTRACT's rule before it becomes a path.
/// By the time a runner sees a locator it is data that came back from the
/// control plane, and a path it could steer is a path it could steer anywhere.
/// </para>
/// </remarks>
public sealed class FileCredentialStore : ICredentialStore
{
    /// <summary>What a credential written before slice fifty-nine is kept in.</summary>
    /// <remarks>
    /// <b>Still read, never written.</b> Every store in the field holds these,
    /// and a store that could only read what it sealed would strand every
    /// machine the day sealing shipped. Step 3 reseals them; until it runs, both
    /// shapes exist and the extension is what tells them apart — which is also
    /// what lets <c>Protection</c> answer per credential rather than for a
    /// directory it has not opened.
    /// </remarks>
    private const string Extension = ".secret";

    /// <summary>What a sealed credential is kept in.</summary>
    /// <remarks>
    /// <b>A different extension rather than a header inside the same file.</b>
    /// Telling the two apart must not require opening either: <c>Holds</c> may
    /// not open one (rule 7), and a plaintext secret can look like anything,
    /// including whatever a header would be.
    /// </remarks>
    private const string SealedExtension = ".sealed";

    private readonly string _root;
    private readonly Lazy<MachineKey> _key;

    /// <summary>
    /// This machine's store.
    /// </summary>
    /// <param name="root">Where the credentials live. Defaults to <see cref="DefaultRoot"/>.</param>
    /// <param name="key">
    /// The key this store seals under. Defaults to this machine's, loaded or
    /// made on first use.
    /// </param>
    /// <remarks>
    /// <b>The key is resolved LAZILY, and that is not an optimisation.</b>
    /// Loading it eagerly would mint one on every construction — including in a
    /// console that only ever asks <c>Holds</c>, and including in a test that
    /// passed a temporary root and would then have written a key into the real
    /// user's configuration directory.
    /// </remarks>
    public FileCredentialStore(string? root = null, MachineKey? key = null)
    {
        _root = root ?? DefaultRoot();
        _key = new Lazy<MachineKey>(() => key ?? MachineKey.LoadOrCreate());
    }

    /// <summary>Where credentials live when nobody overrides it.</summary>
    /// <remarks>
    /// Under the session's own directory, deliberately: one place to find, one
    /// place to back up, and one place to get the permissions wrong.
    /// </remarks>
    public static string DefaultRoot() =>
        Path.Combine(Path.GetDirectoryName(FileSessionStore.DefaultPath())!, "credentials");

    public string Root => _root;

    /// <summary>
    /// What this store is and how it protects what it holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It counts, rather than describing an intention.</b> A machine
    /// mid-migration holds both shapes, and a sentence that said "sealed"
    /// over a directory with plaintext in it would be exactly the lie this
    /// property's own rule forbids.
    /// </para>
    /// <para>
    /// <b>The honesty clause survives the whole slice.</b> The key is a file
    /// this machine can read, so sealing buys nothing against anything already
    /// running as this user — what it buys is that a directory which MOVES
    /// opens nowhere. ADR-0037 requires that distinction keep being said in as
    /// many words.
    /// </para>
    /// </remarks>
    public string Protection
    {
        get
        {
            var plaintext = Counted(Extension);
            var sealedUp = Counted(SealedExtension);
            var elsewhere = SealedToSomebodyElse();
            var mine = sealedUp - elsewhere;

            var said = $"a file per credential under {_root}, mode 0600 in a mode-0700 directory. ";

            // COUNTED, NOT CLAIMED, and now in two kinds rather than one. Before
            // step 2 everything here was sealed to this machine, so one clause was
            // true of the whole directory. A credential sealed to a PERSON is not
            // openable by anything on this machine - a stronger property than the
            // machine-sealed one - and a sentence that named only the majority
            // would be the lie this property's own rule forbids.
            if (mine > 0)
            {
                said += $"{mine} sealed to this machine's own key: anything running as this user "
                      + "can read that key and open them, and what sealing adds is that a copy of "
                      + "this directory alone - a backup, a disk image, a support bundle - opens "
                      + "nowhere. ";
            }

            if (elsewhere > 0)
            {
                said += $"{elsewhere} sealed to somebody else - this machine cannot open them at "
                      + "all, with or without its own key. `gg credential list` names who can, and "
                      + "`gg credential trust-this-machine <locator>` is how a holder adds this "
                      + "machine. ";
            }

            if (mine == 0 && elsewhere == 0 && plaintext == 0)
            {
                said += "Nothing is here yet. ";
            }

            return plaintext == 0
                ? said.TrimEnd()
                : said
                + $"{plaintext} of {plaintext + sealedUp} here are still plaintext from before "
                + "sealing; each is resealed the next time it is read.";
        }
    }

    /// <summary>
    /// How many sealed credentials here this machine is not a holder of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It opens nothing</b>, which is what makes the sentence safe to print
    /// anywhere. Whether this machine is a holder is a question about the wrapped
    /// keys in an envelope, and <c>WrappedFor</c> answers it by comparing public
    /// keys — so <c>gg doctor</c> needs no passphrase, and a diagnostic that needed
    /// one would be a diagnostic nobody runs.
    /// </para>
    /// <para>
    /// <b>An unreadable envelope counts as not-mine.</b> A file that will not parse
    /// is one this machine certainly cannot open, and the plaintext clause beside
    /// this is where a damaged store gets described. Throwing from a sentence about
    /// a directory would take `gg doctor` down over exactly the machine it is most
    /// needed on.
    /// </para>
    /// </remarks>
    private int SealedToSomebodyElse()
    {
        if (!Directory.Exists(_root))
        {
            return 0;
        }

        var mine = _key.Value.PublicKey;
        var count = 0;

        foreach (var file in Directory.EnumerateFiles(
                     _root, "*" + SealedExtension, SearchOption.AllDirectories))
        {
            try
            {
                var envelope = JsonSerializer.Deserialize(
                    File.ReadAllText(file), SealedCredentialJson.Default.SealedCredential);

                if (envelope is null || SealedCredential.WrappedFor(envelope, mine) is null)
                {
                    count++;
                }
            }
            catch (Exception failure) when (
                failure is JsonException or IOException or UnauthorizedAccessException)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>How many credentials under this root are kept in one shape.</summary>
    private int Counted(string extension) =>
        Directory.Exists(_root)
            ? Directory.EnumerateFiles(_root, "*" + extension, SearchOption.AllDirectories).Count()
            : 0;

    /// <summary>
    /// The shape on disk, which is the one decision both answers come from.
    /// </summary>
    /// <remarks>
    /// <b>The extension IS the answer</b>, so this opens nothing —
    /// <see cref="Holds"/>'s guarantee, and the reason a sentence about a
    /// credential is never a reason to decrypt one.
    /// </remarks>
    public string RestingOf(string locator) =>
        SealedIsCurrent(locator) ? CredentialResting.Sealed
        : File.Exists(PathFor(locator)) ? CredentialResting.Plaintext
        : CredentialResting.NotHere;

    /// <summary>
    /// Whether the sealed entry is the value this locator holds.
    /// </summary>
    /// <remarks>
    /// <b>THE NEWER FILE IS THE VALUE.</b> A binary from before sealing sees no
    /// <c>.sealed</c> file, so when a person rotates a token with one it writes a
    /// plaintext file beside the sealed entry it cannot see. Preferring the sealed
    /// entry regardless then answers every read with the token that was replaced -
    /// measured: a tracker's reader returned 401 for days while the rotated token
    /// sat one file over, and re-adding it changed nothing. A plaintext file
    /// written AFTER the envelope is the later act, so it wins and is resealed on
    /// the read, which also deletes it.
    /// </remarks>
    private bool SealedIsCurrent(string locator)
    {
        var sealedPath = SealedPathFor(locator);

        if (!File.Exists(sealedPath))
        {
            return false;
        }

        var plaintext = PathFor(locator);

        return !File.Exists(plaintext)
            || File.GetLastWriteTimeUtc(plaintext) <= File.GetLastWriteTimeUtc(sealedPath);
    }

    /// <summary>
    /// The holders named by the envelope here, or empty when there is none.
    /// </summary>
    /// <remarks>
    /// <b>A damaged envelope names nobody rather than throwing.</b> The bytes came
    /// from another machine, and a pane that asked this on every refresh would be
    /// a pane one truncated file takes down. <c>RestingOf</c> still says the
    /// credential is here, and opening it will say what is wrong with it.
    /// </remarks>
    public IReadOnlyList<string> HoldersOf(string locator)
    {
        // A stale envelope's holders are not this value's: the plaintext beside it
        // is what Read answers, and plaintext is sealed to nobody.
        if (!SealedIsCurrent(locator))
        {
            return [];
        }

        try
        {
            return [.. SealedFor(locator).Wrapped.Select(w => w.Holder)];
        }
        catch (CredentialUnavailableException)
        {
            return [];
        }
    }

    // ONE DECISION, TWO LENGTHS. The sentence is a switch over the word rather
    // than a second look at the filesystem, so a reworded sentence cannot come
    // to disagree with the column beside it about whether a secret is encrypted.
    public string ProtectionFor(string locator) => RestingOf(locator) switch
    {
        CredentialResting.Sealed =>
            "sealed to this machine's own key. Anything running as this user can read that "
          + "key and open it; a copy of the file alone opens nowhere.",
        CredentialResting.Plaintext =>
            "plaintext, from before this machine sealed anything. Anything that can read the "
          + "file can read the secret; it is resealed the next time it is read.",
        _ => $"nothing is stored here for '{locator}'.",
    };

    public string PathFor(string locator)
    {
        if (CredentialLocator.Validate(locator) is { } problem)
        {
            // Refused rather than sanitised. Sanitising means deciding what
            // somebody meant by "../../etc/passwd", and there is no answer to
            // that question that is better than saying no.
            throw new ArgumentException(problem, nameof(locator));
        }

        var body = locator[CredentialLocator.LocalPrefix.Length..];
        var path = Path.GetFullPath(Path.Combine(
            _root, Path.Combine([.. body.Split('/')]) + Extension));

        // Belt and braces. The contract's charset already makes this
        // unreachable; the check costs nothing and this is the one place where
        // being wrong writes a file somewhere else on the machine.
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException($"'{locator}' resolves outside the store.", nameof(locator));
    }

    /// <summary>Where a SEALED credential for this locator is kept.</summary>
    /// <remarks>
    /// Public because a test has to be able to damage one, and because
    /// <c>gg doctor</c> reports per credential in step 3. It derives from
    /// <see cref="PathFor"/> so there is one containment check rather than two.
    /// </remarks>
    public string SealedPathFor(string locator) =>
        Path.ChangeExtension(PathFor(locator), SealedExtension);

    /// <summary>
    /// The sealed entry for this locator, without opening it.
    /// </summary>
    /// <remarks>
    /// <b>How a push gets something to rewrap.</b> It reads the envelope rather
    /// than the value, which is the whole of ADR-0037 Decision 3: the machine
    /// doing the pushing unwraps thirty-two bytes and never the credential.
    /// Throws for a locator this machine holds only in plaintext, because there
    /// is no envelope to hand on — reading it would be a decrypt, and the
    /// caller must know the difference.
    /// </remarks>
    public SealedCredential SealedFor(string locator)
    {
        var path = SealedPathFor(locator);

        if (!File.Exists(path))
        {
            throw new CredentialUnavailableException(
                $"There is no sealed credential at '{locator}' on this machine. "
              + "Read it once to reseal it, or add it here.");
        }

        return Envelope(locator, File.ReadAllText(path));
    }

    /// <summary>
    /// Writes an envelope exactly as it arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Unopened, which is what makes a push cost nothing in plaintext at
    /// EITHER end.</b> The sender rewrapped without decrypting; a receiver that
    /// opened this to re-seal it under its own store would undo that on arrival
    /// and leave the credential in the clear on a machine nobody is watching.
    /// </para>
    /// <para>
    /// <b>It takes what it is given rather than judging it.</b> A machine may be
    /// handed an envelope sealed to somebody else, and refusing would be the
    /// store deciding what it is allowed to hold. <see cref="Read"/> is where
    /// rule 9's diagnosis belongs, and it already names the locator and never
    /// the bytes.
    /// </para>
    /// </remarks>
    public void WriteSealed(string locator, SealedCredential envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        WriteEnvelope(locator, envelope);
    }

    public void Write(string locator, string secret) =>
        WriteEnvelope(locator, CredentialSeal.Seal(secret, [_key.Value.PublicKey]));

    public void Register(string locator, string secret, string holder)
    {
        ArgumentException.ThrowIfNullOrEmpty(holder);

        WriteEnvelope(locator, CredentialSeal.Seal(secret, [holder]));
    }

    public void TrustThisMachine(string locator, IAgreeAsAHolder person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var envelope = SealedFor(locator);

        if (SealedCredential.WrappedFor(envelope, _key.Value.PublicKey) is not null)
        {
            // ALREADY TRUSTED, WHICH IS NOT A FAILURE. Somebody who cannot
            // remember whether they ran this has asked for a state that already
            // obtains, and Rewrap's refusal - right for a caller choosing a
            // recipient - would read here as "something is wrong".
            return;
        }

        try
        {
            WriteSealed(locator, CredentialSeal.Rewrap(envelope, person, _key.Value.PublicKey));
        }
        catch (CryptographicException refused)
        {
            // NOT SEALED TO YOU IS NOT CORRUPT, reaching the local act. The inner
            // sentence names the holders and never the bytes, and sends somebody
            // to ask for a push rather than to suspect the file.
            throw new CredentialUnavailableException(
                $"The credential at '{locator}' cannot be opened by that key, so this machine "
              + "cannot be made a holder of it. " + refused.Message);
        }
    }

    /// <summary>
    /// Puts an envelope on disk, locked down, and takes the plaintext with it.
    /// </summary>
    /// <remarks>
    /// <b>One path for a sealed write, whether the envelope was made here or
    /// handed over.</b> Two would be two places to get the permissions, the
    /// directory modes and the plaintext delete right, and the one that was
    /// wrong would be the one nobody exercised.
    /// </remarks>
    private void WriteEnvelope(string locator, SealedCredential envelope)
    {
        var path = SealedPathFor(locator);
        var plaintext = PathFor(locator);

        // Every directory from the store's root down, not just the leaf. A
        // locator with a slash in it creates an intermediate directory, and one
        // created with the default mode is exactly as readable as the umask
        // happens to be.
        foreach (var directory in DirectoriesTo(Path.GetDirectoryName(path)!))
        {
            Directory.CreateDirectory(directory);
            RestrictDirectory(directory);
        }

        // Created empty and locked down BEFORE the secret goes in, so there is
        // no instant in which a readable file holds one. The session store does
        // the same thing for the same reason.
        if (!File.Exists(path))
        {
            using (File.Create(path)) { }
        }
        RestrictFile(path);

        File.WriteAllText(
            path, JsonSerializer.Serialize(envelope, SealedCredentialJson.Default.SealedCredential));

        RestrictFile(path);

        // THE PLAINTEXT GOES, and this line is the whole of S59.2-01's teeth.
        // Sealing on write while leaving the old file where it sat passes every
        // round-trip test anybody would write and leaves the value on disk for
        // ever, for whoever copies the directory.
        File.Delete(plaintext);
    }

    public string? Read(string locator)
    {
        if (SealedIsCurrent(locator))
        {
            return Open(locator, File.ReadAllText(SealedPathFor(locator)));
        }

        var path = PathFor(locator);

        // A missing secret is a diagnosis the caller makes - doctor reports it,
        // the runner turns it into a flight-log event - not an exception thrown
        // from a file API somewhere down the stack.
        if (!File.Exists(path))
        {
            return null;
        }

        var secret = File.ReadAllText(path);
        Reseal(locator, secret);
        return secret;
    }

    /// <summary>
    /// Writes a plaintext credential back sealed, if it can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On read, rather than by a verb somebody has to run.</b> A migration
    /// that needs a person reaches the machines whose operator reads release
    /// notes and no others — on a fleet that means the pool hosts migrate and
    /// the laptops do not. Reading is the one thing that certainly happens to a
    /// credential anybody still uses.
    /// </para>
    /// <para>
    /// <b>BEST EFFORT, and that is the half that keeps a fleet up.</b> A
    /// read-only mount, a full disk, a directory somebody chmodded: none of them
    /// may turn a credential that resolves perfectly well into a failed flight.
    /// The value has already been read by the time this runs, and the caller
    /// gets it whatever happens here.
    /// </para>
    /// <para>
    /// <b>Nothing is said when it fails.</b> There is no caller who could act on
    /// it — a runner mid-flight cannot fix a disk — and a line per read on a
    /// machine that cannot write would be a log nobody reads full of a fact
    /// <c>gg doctor</c> already states calmly, once, on request.
    /// </para>
    /// </remarks>
    private void Reseal(string locator, string secret)
    {
        try
        {
            Write(locator, secret);
        }
        catch (Exception failure) when (
            failure is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // The plaintext stays where it is and reads again next time.
        }
    }

    /// <summary>
    /// The value inside a sealed entry, or a refusal that is not "absent".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rule 9: a credential that cannot be opened is not one that is
    /// absent.</b> Null means "no credential for that locator on this machine",
    /// and a runner handed that clones anonymously — so a store carried to
    /// another machine would quietly become a flight with no permissions
    /// instead of a diagnosis somebody can act on.
    /// </para>
    /// <para>
    /// <b>Rule 8: the sentence names the locator and never what it found.</b>
    /// This is reached holding a ciphertext and a wrapped key, and an exception
    /// that helpfully included either would print both into a console and a
    /// flight log. <see cref="CredentialUnavailableException"/> is already the
    /// type for "this machine cannot get it, with a sentence", and
    /// <c>LocalCredentialResolver</c> already turns it into a flight's
    /// diagnosis.
    /// </para>
    /// </remarks>
    /// <summary>The envelope on disk, deserialised and not opened.</summary>
    /// <remarks>
    /// <b>Split from <see cref="Open"/> so a push can read what it will rewrap
    /// without decrypting it.</b> The two used to be one method, which was fine
    /// while the only reason to read a file was to get the value out of it -
    /// and is exactly what Decision 3 needs apart.
    /// </remarks>
    private static SealedCredential Envelope(string locator, string written)
    {
        try
        {
            return JsonSerializer.Deserialize(written, SealedCredentialJson.Default.SealedCredential)
                ?? throw new JsonException("null");
        }
        catch (JsonException)
        {
            throw new CredentialUnavailableException(
                $"The credential at '{locator}' on this machine is not a sealed credential. "
              + "Something truncated or overwrote it; push it here again.");
        }
    }

    private string Open(string locator, string written)
    {
        var envelope = Envelope(locator, written);

        try
        {
            return CredentialSeal.Open(envelope, _key.Value.ForOpeningWhatThisMachineSealed());
        }
        catch (CryptographicException refused)
        {
            // THE INNER SENTENCE IS ALREADY SAFE. CredentialSeal says which
            // holders an envelope is for - public keys, and the fact somebody
            // needs to work out who can push it to them - and never the bytes.
            //
            // AND THE ACT THAT WOULD FIX IT IS NAMED, because after step 2 the
            // ordinary state of a credential on the machine that registered it is
            // present-and-unopenable-here. "It will not open" sends somebody
            // looking for a damaged file; naming the verb sends them to one
            // command. This is never "absent": Read returns null for that, and
            // conflating the two is an afternoon spent on a file that was fine.
            throw new CredentialUnavailableException(
                $"The credential at '{locator}' is on this machine and will not open here. "
              + refused.Message
              + $" If it is sealed to you rather than to this machine, "
              + $"`gg credential trust-this-machine {locator}` makes this machine a holder too.");
        }
    }

    // THE FILE IS NEVER OPENED. That is the whole difference from Read, and it
    // is why an answer from here may travel somewhere an answer from there may
    // not.
    //
    // AND SEALING IS WHERE THAT WOULD MOST EASILY BE LOST. The obvious
    // implementation over a sealed store is "try to open it and see", which is
    // correct, is easy, and hands the console back the thing this split exists
    // to keep away from it. Presence is the file existing, whatever is in it.
    public bool Holds(string locator) =>
        File.Exists(SealedPathFor(locator)) || File.Exists(PathFor(locator));

    /// <summary>
    /// Deletes it, in whichever shapes it is here in.
    /// </summary>
    /// <remarks>
    /// <b>BOTH, and never one.</b> A machine mid-migration can hold a sealed
    /// entry and a plaintext one for the same locator, and a remove that took
    /// only the sealed half would report true and leave the readable copy behind
    /// — which is the shape of the defect this slice exists to end, arriving
    /// through the verb that is supposed to clean up after it.
    /// </remarks>
    public bool Remove(string locator)
    {
        var removed = false;

        foreach (var path in new[] { SealedPathFor(locator), PathFor(locator) })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                removed = true;
            }
        }

        return removed;
    }

    /// <summary>The store root, then each directory below it, outermost first.</summary>
    private IEnumerable<string> DirectoriesTo(string leaf)
    {
        var root = Path.GetFullPath(_root);
        var chain = new List<string>();

        for (var current = leaf; current is not null && current.Length >= root.Length;
             current = Path.GetDirectoryName(current))
        {
            chain.Add(current);
            if (string.Equals(current, root, StringComparison.Ordinal))
            {
                break;
            }
        }

        chain.Reverse();
        return chain;
    }

    private static void RestrictFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>
    /// 0700 on the directory as well as 0600 on the file.
    /// </summary>
    /// <remarks>
    /// A locked file inside a world-readable directory still tells everyone
    /// which repositories this developer holds credentials for, which is a
    /// fact about the customer nobody agreed to publish.
    /// </remarks>
    private static void RestrictDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}

/// <summary>
/// How a sealed credential is written to this machine's disk.
/// </summary>
/// <remarks>
/// <b>Source-generated, because everything here must stay AOT-publishable.</b>
/// Its own context rather than a shared one: what a store writes to a local file
/// is not what crosses a wire, and a single context covering both would make a
/// change to either able to move the other.
/// </remarks>
[JsonSerializable(typeof(SealedCredential))]
internal sealed partial class SealedCredentialJson : JsonSerializerContext;
