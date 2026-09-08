using System.Security.Cryptography;

namespace Gg.Client;

/// <summary>
/// The key a runner is reached by, made once and kept.
/// </summary>
/// <remarks>
/// <para>
/// <b>Made at registration, because that is the moment trust collapses to.</b>
/// ADR-0013: a key handed over per-session could be substituted per-session, and
/// pinning at registration means a console has one thing to have got right. The
/// public half goes with the registration; the private half never leaves this
/// machine.
/// </para>
/// <para>
/// <b>One key per runner NAME, not per machine.</b> A pool host runs
/// <c>gg runner up</c> as itself, <c>gg runner maintain</c> as
/// <c>&lt;machine&gt;:maintain</c>, and a hand-flight as
/// <c>&lt;machine&gt;:hand</c> — three registrations, three rows, three keys.
/// Sharing one would make a console pinning "this runner" actually pin "this
/// host", and the defect <see cref="FileRunnerStore.PathFor"/> exists to have
/// fixed would arrive again wearing cryptography.
/// </para>
/// <para>
/// <b>ECDH rather than a signing key, because sealing is what it is for.</b> A
/// console seals an offer TO this key; nothing ever asks the runner to sign
/// anything with it. Choosing a signature algorithm here would be choosing for a
/// use nobody has.
/// </para>
/// <para>
/// <b>Beside the runner credential, and it is the same kind of thing at
/// rest.</b> A private key and a thirty-day bearer token are both material this
/// machine must keep, and splitting them across two directories would mean two
/// places to get permissions right.
/// </para>
/// </remarks>
public sealed class RunnerIdentityKey
{
    private readonly ECDiffieHellman _key;

    private RunnerIdentityKey(ECDiffieHellman key) => _key = key;

    /// <summary>
    /// Where the key for a runner registered under <paramref name="name"/> is kept.
    /// </summary>
    /// <remarks>
    /// <b>The credential's own file name with a prefix, rather than a second
    /// sanitiser.</b> A runner name can carry a colon and has to survive being a
    /// file name on every platform; writing that logic twice is how two names
    /// end up flattening to one path, which is the defect
    /// <see cref="FileRunnerStore.PathFor"/> exists to have fixed. So this
    /// reuses it and reads a little oddly - <c>key-runner-laptop.json</c> - and
    /// that is the better trade.
    /// </remarks>
    public static string PathFor(string name) =>
        Path.Combine(
            Path.GetDirectoryName(FileRunnerStore.PathFor(name))!,
            "key-" + FileRunnerStore.FileNameFor(name));

    /// <summary>
    /// This machine's key for one runner name, made on first use.
    /// </summary>
    /// <remarks>
    /// <b>Load or create, in one call, for the pin store's reason.</b> A
    /// separate "make one if there is none" step is one a caller can forget, and
    /// forgetting it would register a runner with no key — which the control
    /// plane accepts and then refuses to introduce, a failure that shows up much
    /// later and somewhere else.
    /// </remarks>
    public static RunnerIdentityKey LoadOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path))
        {
            var key = ECDiffieHellman.Create();
            key.ImportPkcs8PrivateKey(Convert.FromBase64String(File.ReadAllText(path).Trim()), out _);
            return new RunnerIdentityKey(key);
        }

        var made = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Convert.ToBase64String(made.ExportPkcs8PrivateKey()));
        Restrict(path);

        return new RunnerIdentityKey(made);
    }

    /// <summary>What the control plane records and a console seals to.</summary>
    public string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    /// <summary>
    /// The half that opens what a console sealed. It stays on this machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Handed to the runner loop by the composition root, and to nothing
    /// else.</b> <c>Gg.Runner</c> cannot see this project — the runner is treated
    /// as hostile and the reference graph keeps them apart — so <c>Gg.Cli</c>,
    /// which is the only project that sees both, passes it. That is the same
    /// route the takeover reader takes, and for the same reason.
    /// </para>
    /// <para>
    /// <b>Named rather than exposed as a property.</b> Reaching for a private key
    /// should read as an act at the call site: this returns the live object, not
    /// a copy, and whoever takes it can open every offer ever sealed to this
    /// runner.
    /// </para>
    /// </remarks>
    public ECDiffieHellman ForOpeningWhatWasSealedToThisRunner() => _key;

    /// <summary>
    /// Nobody but this account, where the platform can say so.
    /// </summary>
    /// <remarks>
    /// <b>Best effort, and it does not fail the runner.</b> A pool host runs the
    /// runner as its own account and the directory is already the credential's;
    /// on Windows there is no chmod and the call is skipped. Refusing to start
    /// over a permission bit would take a fleet down for a property the
    /// surrounding directory already provides.
    /// </remarks>
    private static void Restrict(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // The directory is the credential's already.
        }
    }
}
