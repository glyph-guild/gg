using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// A person's key, wrapped by a passphrase, kept on their own machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first key in this system that belongs to a PERSON.</b> Everything
/// else is per-machine or per-exchange — a runner's identity key, a console's
/// ephemeral key, the pins, a store's <see cref="MachineKey"/>. A person has
/// had a twelve-hour bearer token and nothing else, and
/// <c>SessionStore</c> is candid that *"the OS keychain is genuinely better and
/// is deliberately NOT built here."*
/// </para>
/// <para>
/// <b>It gates distribution and nothing else.</b> This key seals a credential
/// the first time, pushes one, and adds a holder (ADR-0037 Decisions 2 and 3).
/// It is NOT needed to use one — a machine opens what it holds unattended — and
/// that is what lets a flight run at three in the morning.
/// </para>
/// <para>
/// <b>It never hands itself out, and that one rule keeps a hardware key
/// possible.</b> Every public entry point on <see cref="RunnerSeal"/> takes a
/// concrete <see cref="ECDiffieHellman"/>, and a PIV-backed key is not one —
/// .NET cannot construct that object over a key it does not hold. Those
/// signatures are right for MACHINE keys. If a person's key reached the push
/// path the same way, hardware would be closed and reopening it would mean
/// changing a type both repositories share. So the agreement happens in here
/// and what leaves is bytes.
/// </para>
/// </remarks>
public sealed class PersonKey
{
    /// <summary>The only derivation this build writes.</summary>
    /// <remarks>
    /// <b>PBKDF2 because the BCL has nothing better, measured rather than
    /// assumed.</b> There is no Argon2 and no scrypt in
    /// <c>System.Security.Cryptography</c> on this SDK — checked against the
    /// assembly — and a third-party KDF is a package <c>Gg.Contracts</c> forbids
    /// and AOT makes risky. PBKDF2 is weaker against a GPU than a memory-hard
    /// function; what bounds that is where this file lives. An attacker needs
    /// the wrapped key AND the passphrase, and holding the file already means
    /// running as this user — which ADR-0037 says sealing never defended
    /// against.
    /// </remarks>
    public const string Derivation = "pbkdf2-hmac-sha256";

    /// <summary>
    /// What this build wraps a new key under.
    /// </summary>
    /// <remarks>
    /// <b>Written into the file, never assumed when reading one.</b> A cost
    /// compiled in could only be raised by a release that cannot open any key
    /// written before it, so it would never be raised and the number chosen on
    /// the first day would be the number in the field for ever.
    /// </remarks>
    /// <remarks>
    /// <b><c>static readonly</c> rather than <c>const</c>, on purpose.</b> A
    /// public constant is inlined into everything compiled against it, so
    /// raising the cost would leave every already-built caller still writing the
    /// old one — which is the same orphaning this member exists to prevent,
    /// arriving through the compiler instead of through a file format.
    /// </remarks>
    public static readonly int Iterations = 600_000;

    private const int SaltBytes = 16;

    /// <summary>What a wrapped key is derived for, so it opens nothing else.</summary>
    private const string WrapLabel = "gg-person-key-at-rest";

    private readonly ECDiffieHellman _key;

    private PersonKey(ECDiffieHellman key, string publicKey)
    {
        _key = key;
        PublicKey = publicKey;
    }

    /// <summary>Where a person's key lives when nobody overrides it.</summary>
    /// <remarks>
    /// Beside the session and the machine key — one directory a person has to
    /// find exactly once, which is <c>FileCredentialStore</c>'s judgement and
    /// Article X's "prefer fewer components" applied to a filesystem.
    /// </remarks>
    public static string DefaultPath() =>
        Path.Combine(Path.GetDirectoryName(FileSessionStore.DefaultPath())!, "person-key");

    /// <summary>What a credential is sealed to for this person. Not a secret.</summary>
    public string PublicKey { get; }

    /// <summary>Mints a key and wraps it under a passphrase.</summary>
    /// <remarks>
    /// <b>It refuses to overwrite.</b> A key that is replaced is every
    /// credential sealed to it lost, with no undo and no escrow (Decision 8), so
    /// this has to be an act somebody takes deliberately rather than one that
    /// `gg key create` run twice performs quietly.
    /// </remarks>
    public static PersonKey Create(string? path = null, string passphrase = "") =>
        CreateAtCostForTesting(path, passphrase, Iterations);

    /// <summary>
    /// <see cref="Create"/>, at a stated cost.
    /// </summary>
    /// <remarks>
    /// <b>Public only so a test can prove the cost is READ from the file</b>
    /// rather than assumed from <see cref="Iterations"/> — which is the single
    /// property that lets the number be raised later without orphaning every key
    /// already written. Nothing in the product calls it with anything but
    /// <see cref="Iterations"/>, and it hands out no key material, so it is not
    /// the escape hatch rule 3 forbids.
    /// </remarks>
    public static PersonKey CreateAtCostForTesting(string? path, string passphrase, int iterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var at = path ?? DefaultPath();

        if (File.Exists(at))
        {
            throw new InvalidOperationException(
                $"There is already a key at '{at}'. Replacing it would make every credential "
              + "sealed to it unopenable, and gg keeps no copy that could bring one back.");
        }

        var made = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var wrapping = Wrapping(passphrase, salt, iterations);

        try
        {
            var stored = new StoredPersonKey
            {
                Derivation = Derivation,
                Iterations = iterations,
                Salt = Convert.ToBase64String(salt),
                Wrapped = Convert.ToBase64String(
                    RunnerSeal.SealUnder(wrapping, made.ExportPkcs8PrivateKey())),
                PublicKey = Convert.ToBase64String(made.ExportSubjectPublicKeyInfo()),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(at)!);

            using (File.Create(at)) { }
            Restrict(at);

            File.WriteAllText(at, JsonSerializer.Serialize(stored, PersonKeyJson.Default.StoredPersonKey));
            Restrict(at);

            return new PersonKey(made, stored.PublicKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapping);
        }
    }

    /// <summary>The public half, without unlocking anything.</summary>
    /// <remarks>
    /// A public key is not a secret, and somebody has to be able to register it,
    /// print it and be pushed to at it without typing a passphrase.
    /// </remarks>
    public static string PublicHalfOf(string? path = null) => Read(path).PublicKey;

    /// <summary>The salt a key was wrapped under. Public, and not a secret.</summary>
    public static string SaltOf(string? path = null) => Read(path).Salt;

    /// <summary>Opens the key with the passphrase that wrapped it.</summary>
    /// <remarks>
    /// <para>
    /// <b>The cost and salt come from the FILE.</b> That is the whole of
    /// S59.4-02: a key wrapped at any cost this build can compute must open,
    /// whether that cost is higher or lower than the one it writes today.
    /// </para>
    /// <para>
    /// <b>A wrong passphrase and a damaged file read the same, on purpose.</b>
    /// AES-GCM cannot tell them apart — a bad key and a flipped byte both fail
    /// the tag — so a sentence that guessed would be confidently wrong half the
    /// time. It names neither, and never repeats what it was given.
    /// </para>
    /// </remarks>
    public static PersonKey Unlock(string? path = null, string passphrase = "")
    {
        var stored = Read(path);

        if (!string.Equals(stored.Derivation, Derivation, StringComparison.Ordinal))
        {
            // NAMED, for the sealed envelope's reason: a file from a later gg met
            // by an earlier one sends somebody to update rather than to doubt
            // their passphrase. A derivation name is not a secret.
            throw new CredentialUnavailableException(
                $"This key is wrapped with '{stored.Derivation}', and this gg can open "
              + $"'{Derivation}'. Update gg on this machine.");
        }

        var wrapping = Wrapping(
            passphrase, Convert.FromBase64String(stored.Salt), stored.Iterations);

        try
        {
            var key = ECDiffieHellman.Create();
            key.ImportPkcs8PrivateKey(
                RunnerSeal.OpenUnder(wrapping, Convert.FromBase64String(stored.Wrapped)), out _);

            return new PersonKey(key, stored.PublicKey);
        }
        catch (Exception failure) when (failure is CryptographicException or FormatException)
        {
            throw new CredentialUnavailableException(
                "That did not open this key. Either the passphrase is wrong or the file has been "
              + "damaged; nothing here can tell which, and gg keeps no copy that could.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapping);
        }
    }

    /// <summary>
    /// Agrees a key with someone else's public half, and returns the bytes.
    /// </summary>
    /// <remarks>
    /// <b>THE ADAPTER'S ONE JOB.</b> The private half never leaves this object —
    /// no property, no accessor, no overload taking or returning an
    /// <see cref="ECDiffieHellman"/>. That is what keeps a PIV-backed
    /// implementation of this same method possible, and it costs nothing today,
    /// which is exactly why only a failing test keeps it.
    /// </remarks>
    public byte[] AgreeWith(string theirPublicKey, string label)
    {
        using var peer = ECDiffieHellman.Create();
        peer.ImportSubjectPublicKeyInfo(Convert.FromBase64String(theirPublicKey), out _);

        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            _key.DeriveRawSecretAgreement(peer.PublicKey),
            32,
            info: Encoding.UTF8.GetBytes(label));
    }

    private static StoredPersonKey Read(string? path)
    {
        var at = path ?? DefaultPath();

        if (!File.Exists(at))
        {
            throw new CredentialUnavailableException(
                $"There is no key at '{at}'. Run `gg key create` to make one.");
        }

        return JsonSerializer.Deserialize(File.ReadAllText(at), PersonKeyJson.Default.StoredPersonKey)
            ?? throw new CredentialUnavailableException(
                $"The key at '{at}' is not one gg wrote. Something overwrote it.");
    }

    private static byte[] Wrapping(string passphrase, byte[] salt, int iterations) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passphrase), salt, iterations, HashAlgorithmName.SHA256, 32),
            32,
            info: Encoding.UTF8.GetBytes(WrapLabel));

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
            // The directory's own mode is the backstop, as it is for every other
            // key this machine keeps.
        }
    }
}

/// <summary>A person's key as it rests on disk.</summary>
/// <remarks>
/// <b>Not a wire type.</b> It never crosses to the control plane — only the
/// public half does, and that is a different type on the contract. So it carries
/// no pinned id and is not in the vocabulary, and the parameters are members
/// rather than constants because reading them is the point.
/// </remarks>
internal sealed record StoredPersonKey
{
    public required string Derivation { get; init; }
    public required int Iterations { get; init; }
    public required string Salt { get; init; }
    public required string Wrapped { get; init; }
    public required string PublicKey { get; init; }
}

[JsonSerializable(typeof(StoredPersonKey))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
internal sealed partial class PersonKeyJson : JsonSerializerContext;
