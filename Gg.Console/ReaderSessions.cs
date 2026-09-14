using Gg.Local;

namespace Gg.Console;

/// <summary>
/// The readers this console can browse, one process each, owned here.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>LiveTails</c>' shape, and for its reason.</b> <c>IUiSession</c> must
/// not retain anything across calls: this object outlives the session, is owned
/// by whoever composed the console, and the session merely calls it. A process
/// handle that must survive a UI rebuild is precisely something that should not
/// live on a session.
/// </para>
/// <para>
/// <b>One per provider key, because a tenant may configure several</b> - and
/// because the alternative is a process per keystroke. A reader asked for twice
/// is the same reader.
/// </para>
/// <para>
/// <b>A key nothing declares answers null</b>, which is a different sentence
/// from a reader that cannot browse. The first is a tracker this machine was
/// never told about; the second is one that does not do listings.
/// <c>IntentConfiguration.Unreadable</c> is where the first is worded.
/// </para>
/// </remarks>
public sealed class ReaderSessions(
    IReadOnlyList<IntentReader> readers, TimeSpan patience) : IAsyncDisposable
{
    private readonly Dictionary<string, SpawnedReader> _started = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<IntentReader> _readers = readers;
    private readonly TimeSpan _patience = patience;

    /// <summary>The provider keys this console could browse, if asked.</summary>
    public IReadOnlyList<string> Keys => [.. _readers.Select(reader => reader.Key)];

    /// <summary>The reader for a key, started or not, or null if none is declared.</summary>
    public SpawnedReader? For(string providerKey)
    {
        if (_started.TryGetValue(providerKey, out var already))
        {
            return already;
        }

        var declared = _readers.FirstOrDefault(
            reader => string.Equals(reader.Key, providerKey, StringComparison.Ordinal));

        if (declared.Key is null)
        {
            return null;
        }

        var reader = new SpawnedReader(declared, _patience);
        _started[providerKey] = reader;
        return reader;
    }

    /// <summary>
    /// Stop every reader this console started.
    /// </summary>
    /// <remarks>
    /// <b>All of them, even if one refuses.</b> A disposal that stopped at the
    /// first failure would leave the rest running, which is the exact leak this
    /// exists to prevent.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        foreach (var reader in _started.Values)
        {
            await reader.DisposeAsync();
        }

        _started.Clear();
    }
}
