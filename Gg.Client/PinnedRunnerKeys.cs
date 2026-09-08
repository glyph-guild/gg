using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Client;

/// <summary>What checking a runner's key against the pin found.</summary>
/// <remarks>
/// <b>Three outcomes rather than a boolean, and the third is the whole point.</b>
/// A first introduction and a matching one are both fine and are different
/// facts; a changed key is neither. Collapsing the first two would make "have I
/// met this runner before" unanswerable, and collapsing the last two would be
/// the defect.
/// </remarks>
public enum PinVerdict
{
    /// <summary>Never met. Now pinned.</summary>
    Pinned,

    /// <summary>Met before, and it is the same key.</summary>
    Matches,

    /// <summary>Met before, and it is NOT the same key.</summary>
    Changed,
}

/// <summary>One runner's key, as this machine last saw it.</summary>
public sealed record PinnedKey
{
    [JsonPropertyName("runnerId")]
    public required string RunnerId { get; init; }

    [JsonPropertyName("publicKey")]
    public required string PublicKey { get; init; }

    [JsonPropertyName("pinnedAt")]
    public required DateTimeOffset PinnedAt { get; init; }
}

/// <summary>
/// The runner keys this machine has seen, so a substitution is noticed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because the console learns a runner's key FROM THE CONTROL PLANE.</b>
/// ADR-0013 says identity is pinned at registration so a compromised signalling
/// server cannot substitute fingerprints — and that is weaker than it reads,
/// because the key itself arrives over the introduction. Sealing to whatever key
/// the control plane hands over defends against a network, not against the
/// control plane. Trust on first use is what closes the rest: the first
/// introduction trusts it, and every one after trusts this file.
/// </para>
/// <para>
/// <b>The residual, stated rather than implied.</b> A control plane hostile at
/// the moment of the very first introduction can still substitute. It can also
/// schedule a flight onto that runner and run code there, so it is not the
/// weakest link — but "pinned" should not be read as covering it.
/// </para>
/// <para>
/// <b>Beside the runner credential, in the config root rather than the state
/// root.</b> A pin is not runtime state: losing it silently would make every
/// runner look new, which is the one failure mode that turns this file into
/// decoration.
/// </para>
/// <para>
/// <b>Nothing secret is in here.</b> These are public keys, so the file needs no
/// protection beyond being where a person can find and edit it — which matters,
/// because the way through a refusal has to be something a person can actually
/// do.
/// </para>
/// </remarks>
public sealed class PinnedRunnerKeys
{
    private readonly string _path;

    public PinnedRunnerKeys(string? path = null) => _path = path ?? DefaultPath();

    /// <summary>Beside <see cref="FileRunnerStore.DefaultPath"/>.</summary>
    public static string DefaultPath() =>
        Path.Combine(
            Path.GetDirectoryName(FileRunnerStore.DefaultPath())!, "pinned-runner-keys.json");

    public string FilePath => _path;

    /// <summary>
    /// Checks a runner's offered key against the pin, pinning it on first sight.
    /// </summary>
    /// <remarks>
    /// <b>It pins as a side effect, deliberately.</b> A separate "now record it"
    /// call is one a caller can forget, and the forgetting is invisible: every
    /// introduction would read as a first one and a substitution would never be
    /// noticed. One call that answers and remembers cannot be half-used.
    /// </remarks>
    public PinVerdict Check(string runnerId, string offered, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(offered);

        var pins = Read();

        if (pins.TryGetValue(runnerId, out var known))
        {
            // ORDINAL, because a key is bytes rendered as text and two spellings
            // that differ by case are two different keys.
            return string.Equals(known.PublicKey, offered, StringComparison.Ordinal)
                ? PinVerdict.Matches
                : PinVerdict.Changed;
        }

        Write(pins, runnerId, offered, now);
        return PinVerdict.Pinned;
    }

    /// <summary>
    /// Replaces the pin, which is the one way through a refusal.
    /// </summary>
    /// <remarks>
    /// <b>Explicit, and it is the only way.</b> A reinstall rotates a runner's
    /// key legitimately, so a refusal fires on the common case — and a warning
    /// that fires on the common case is one people learn to click through, which
    /// is how ssh's host-key prompt fails in practice. Refusing and making the
    /// recovery a deliberate act keeps the rare case visible.
    /// </remarks>
    public void Repin(string runnerId, string offered, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(offered);

        Write(Read(), runnerId, offered, now);
    }

    /// <summary>What this machine has pinned, by runner id.</summary>
    public IReadOnlyDictionary<string, PinnedKey> Read()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, PinnedKey>(StringComparer.Ordinal);
        }

        try
        {
            var stored = JsonSerializer.Deserialize(
                File.ReadAllText(_path), PinnedKeyContext.Default.PinnedKeyArray);

            return (stored ?? []).ToDictionary(k => k.RunnerId, k => k, StringComparer.Ordinal);
        }
        catch (Exception failure) when (failure is IOException
                                            or UnauthorizedAccessException
                                            or JsonException)
        {
            // A FILE NOBODY CAN READ IS NOT NO PINS, and this is the one place
            // that distinction would be dangerous to get wrong: returning empty
            // would silently re-pin every runner on the next introduction, which
            // is exactly what an attacker would want the file to do. Loud.
            throw new PinnedKeysUnreadableException(
                $"The pinned runner keys at {_path} could not be read, so this console cannot "
              + "tell a runner it has met from one it has not. That is a state to look at "
              + "rather than to carry on from: fix or delete the file deliberately.",
                failure);
        }
    }

    private void Write(
        IReadOnlyDictionary<string, PinnedKey> pins, string runnerId, string key, DateTimeOffset now)
    {
        var next = pins.Values
            .Where(k => !string.Equals(k.RunnerId, runnerId, StringComparison.Ordinal))
            .Append(new PinnedKey { RunnerId = runnerId, PublicKey = key, PinnedAt = now })
            .OrderBy(k => k.RunnerId, StringComparer.Ordinal)
            .ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(next, PinnedKeyContext.Default.PinnedKeyArray));
    }
}

/// <summary>The pins could not be read, so nothing can be trusted about them.</summary>
public sealed class PinnedKeysUnreadableException(string message, Exception inner)
    : Exception(message, inner);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PinnedKey[]))]
internal sealed partial class PinnedKeyContext : JsonSerializerContext;
