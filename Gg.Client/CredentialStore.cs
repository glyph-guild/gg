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

    /// <summary>Where a locator's secret is kept. Throws if the locator is not one.</summary>
    string PathFor(string locator);

    /// <summary>Stores a secret against a locator, replacing whatever was there.</summary>
    void Write(string locator, string secret);

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

    public string Protection =>
        $"a file per credential under {_root}, mode 0600 in a mode-0700 directory. "
      + "Anything running as this user can read it; what this protects is that the secret "
      + "never reaches the control plane.";

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

    public void Write(string locator, string secret)
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
            path,
            JsonSerializer.Serialize(
                CredentialSeal.Seal(secret, [_key.Value.PublicKey]),
                SealedCredentialJson.Default.SealedCredential));

        RestrictFile(path);

        // THE PLAINTEXT GOES, and this line is the whole of S59.2-01's teeth.
        // Sealing on write while leaving the old file where it sat passes every
        // round-trip test anybody would write and leaves the value on disk for
        // ever, for whoever copies the directory.
        File.Delete(plaintext);
    }

    public string? Read(string locator)
    {
        var sealedPath = SealedPathFor(locator);

        if (File.Exists(sealedPath))
        {
            return Open(locator, File.ReadAllText(sealedPath));
        }

        var path = PathFor(locator);

        // A missing secret is a diagnosis the caller makes - doctor reports it,
        // the runner turns it into a flight-log event - not an exception thrown
        // from a file API somewhere down the stack.
        //
        // STILL READ, NEVER WRITTEN. A credential written before sealing shipped
        // opens as it always did; step 3 is what reseals it.
        return File.Exists(path) ? File.ReadAllText(path) : null;
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
    private string Open(string locator, string written)
    {
        SealedCredential? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize(
                written, SealedCredentialJson.Default.SealedCredential);
        }
        catch (JsonException)
        {
            envelope = null;
        }

        if (envelope is null)
        {
            throw new CredentialUnavailableException(
                $"The credential at '{locator}' on this machine is not a sealed credential. "
              + "Something truncated or overwrote it; push it here again.");
        }

        try
        {
            return CredentialSeal.Open(envelope, _key.Value.ForOpeningWhatThisMachineSealed());
        }
        catch (CryptographicException refused)
        {
            // THE INNER SENTENCE IS ALREADY SAFE. CredentialSeal says which
            // holders an envelope is for - public keys, and the fact somebody
            // needs to work out who can push it to them - and never the bytes.
            throw new CredentialUnavailableException(
                $"The credential at '{locator}' is on this machine and will not open here. "
              + refused.Message);
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
