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
    /// <summary>Every stored secret has this extension, so the directory reads honestly.</summary>
    private const string Extension = ".secret";

    private readonly string _root;

    public FileCredentialStore(string? root = null) => _root = root ?? DefaultRoot();

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

    public void Write(string locator, string secret)
    {
        var path = PathFor(locator);

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

        File.WriteAllText(path, secret);
        RestrictFile(path);
    }

    public string? Read(string locator)
    {
        var path = PathFor(locator);

        // A missing secret is a diagnosis the caller makes - doctor reports it,
        // the runner turns it into a flight-log event - not an exception thrown
        // from a file API somewhere down the stack.
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public bool Remove(string locator)
    {
        var path = PathFor(locator);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
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
