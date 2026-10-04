using System.Security.Cryptography;

namespace Gg.Client;

/// <summary>
/// The key this machine's credential store rests under. One per machine, made
/// on first use.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not <see cref="RunnerIdentityKey"/>, and the difference is load-bearing.</b>
/// That one is deliberately one key per runner NAME — its own remark says
/// sharing a key across names "would make a console pinning 'this runner'
/// actually pin 'this host'". A pool host therefore has three of them, and a
/// laptop that has never run <c>gg runner up</c> has none while
/// <c>gg credential add</c> works there perfectly well. A store is a property of
/// the MACHINE, so it needs a key that is one too.
/// </para>
/// <para>
/// <b>It lives beside the session and NOT under the credential root, which is
/// the whole defence.</b> ADR-0037's falsifier is *"take a copy of a machine's
/// credential directory, carry it to another machine, and read a credential out
/// of it"* — and a key kept inside the directory being copied travels with it,
/// leaving sealing worth nothing a file mode did not already provide.
/// <c>AStoreRestsSealedTests</c> fails if it ever moves inside.
/// </para>
/// <para>
/// <b>Be exact about what this buys.</b> The key is a file this machine can
/// read, so sealing does not defend against anything already running as this
/// user — <c>ICredentialStore.Protection</c> must keep saying so. It defends
/// against everything that moves bytes without being that user: a backup, a
/// disk image, a volume snapshot, a <c>docker cp</c>, a support bundle, a
/// stolen disk.
/// </para>
/// </remarks>
public sealed class MachineKey
{
    private readonly ECDiffieHellman _key;

    private MachineKey(ECDiffieHellman key) => _key = key;

    /// <summary>
    /// Where this machine's key lives when nobody overrides it.
    /// </summary>
    /// <remarks>
    /// Beside the session rather than inside <c>credentials/</c>, for the reason
    /// in this type's own remarks. One directory up is the difference between a
    /// copied store that opens and one that does not.
    /// </remarks>
    public static string DefaultPath() =>
        Path.Combine(Path.GetDirectoryName(FileSessionStore.DefaultPath())!, "machine-key");

    /// <summary>This machine's key, made on first use.</summary>
    /// <remarks>
    /// <b>Load or create in one call</b>, for <see cref="RunnerIdentityKey"/>'s
    /// reason: a separate "make one if there is none" step is one a caller can
    /// forget, and forgetting it here would mean a store that cannot seal on a
    /// machine that has never sealed before — which is every machine, once.
    /// </remarks>
    public static MachineKey LoadOrCreate(string? path = null) =>
        new(KeyAtRest.LoadOrCreate(path ?? DefaultPath()));

    /// <summary>What a credential on this machine is sealed to.</summary>
    public string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    /// <summary>
    /// The half that opens what this machine sealed. It stays here.
    /// </summary>
    /// <remarks>
    /// <b>Named rather than exposed as a property</b>, as the runner's is:
    /// reaching for a private key should read as an act at the call site. This
    /// is the MACHINE's key, so an <see cref="ECDiffieHellman"/> is the right
    /// shape for it — a person's key is never typed as one outside its own
    /// adapter (ADR-0037), because a token-backed key is not one, and that
    /// distinction is only coherent while these two stay separate.
    /// </remarks>
    public ECDiffieHellman ForOpeningWhatThisMachineSealed() => _key;
}
