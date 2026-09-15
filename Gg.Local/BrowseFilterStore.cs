using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Local;

/// <summary>
/// What somebody narrowed a tracker's work list to, as it is kept between
/// console sessions.
/// </summary>
/// <remarks>
/// <b>Three members because a query takes three</b>, and they behave the way
/// the modal's own facets do: an area path and a sprint replace, a set of
/// states accumulates. Null and empty both mean "do not narrow by this" -
/// there is no value that means "everything", because a tracker asked to match
/// items filed at <c>""</c> answers the ones filed nowhere.
/// </remarks>
public sealed record RememberedFilters
{
    /// <summary>The area path, or null for all of them.</summary>
    [JsonPropertyName("area-path")]
    public string? AreaPath { get; init; }

    /// <summary>The sprint, or null.</summary>
    [JsonPropertyName("iteration")]
    public string? Iteration { get; init; }

    /// <summary>The states, or empty for the reader's own default.</summary>
    [JsonPropertyName("states")]
    public IReadOnlyList<string> States { get; init; } = [];

    /// <summary>Whether this narrows anything at all.</summary>
    public bool Narrows =>
        AreaPath is { Length: > 0 } || Iteration is { Length: > 0 } || States.Count > 0;
}

/// <summary>Source-generated, because this ships in a Native AOT binary.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, RememberedFilters>))]
[JsonSerializable(typeof(RememberedFilters))]
internal sealed partial class BrowseFilterJson : JsonSerializerContext;

/// <summary>
/// Where a browse filter is kept, and how it survives closing the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>STATE, NOT CONFIGURATION, and the difference decides the directory.</b>
/// <c>config.json</c> is a document a person opens and hand-edits, and
/// <see cref="ConfigurationFile"/> refuses one carrying an unmapped member
/// precisely so a typo cannot look load-bearing. A file gg rewrites underneath
/// somebody to record where their cursor was is not that kind of file, and
/// putting the two together would mean every filter change produced a diff in a
/// document they are asked to review. This lives beside the transcripts and the
/// live views, under <see cref="LocalPaths.StateRoot"/>.
/// </para>
/// <para>
/// <b>Keyed by the reader, because a filter means nothing anywhere else.</b> An
/// area path is one tenant's tree with their own punctuation in it; carried to a
/// second tracker it matches nothing, and the person sees an empty list with no
/// reason for it. One file holding every tracker, so a machine serving two
/// backlogs keeps them apart - and a write is read-modify-write for the same
/// reason.
/// </para>
/// <para>
/// <b>Nothing here is load-bearing, and the code says so by never throwing.</b>
/// A missing file, a corrupt one, an unreadable directory and a full disk all
/// answer "no filter" or "not written". The console must open, and a record of
/// where a cursor was is not worth a terminal that will not start - which is a
/// different trade from <c>ConfigurationFile</c>, whose refusals are the point.
/// </para>
/// </remarks>
public static class BrowseFilterStore
{
    /// <summary>The file's name inside the state root.</summary>
    private const string FileName = "browse-filters.json";

    /// <summary>Where the file lives.</summary>
    /// <param name="stateHome">
    /// The root, for a caller that has one. Null reads the environment - the
    /// override exists because <c>XDG_STATE_HOME</c> is process-global and a
    /// suite that runs four-wide cannot have one test setting it while another
    /// reads it. <see cref="LocalPaths"/> states the same reason.
    /// </param>
    public static string PathFor(string? stateHome = null) =>
        Path.Combine(LocalPaths.StateRoot(stateHome), FileName);

    /// <summary>
    /// What this tracker was last narrowed to, or nothing.
    /// </summary>
    /// <remarks>
    /// Never throws. A tracker nobody has filtered, a file nobody has written,
    /// and a file somebody has broken are one answer here: narrow by nothing.
    /// </remarks>
    public static RememberedFilters Read(string readerKey, string? stateHome = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readerKey);

        return All(stateHome).TryGetValue(readerKey, out var remembered)
            ? remembered
            : new RememberedFilters();
    }

    /// <summary>
    /// Remembers what this tracker is narrowed to, replacing what it had.
    /// </summary>
    /// <remarks>
    /// <b>Replaces rather than merges</b>, because what is absent from the new
    /// filter is what somebody just took off. Merging would make a cleared
    /// sprint come back on the next write.
    /// </remarks>
    public static void Write(
        string readerKey, RememberedFilters filters, string? stateHome = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readerKey);
        ArgumentNullException.ThrowIfNull(filters);

        // READ, MODIFY, WRITE. The file holds every tracker this machine
        // browses, so writing one as though it were the whole document would
        // make narrowing one backlog forget the other.
        var all = new Dictionary<string, RememberedFilters>(All(stateHome), StringComparer.Ordinal)
        {
            [readerKey] = filters,
        };

        try
        {
            var path = PathFor(stateHome);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            File.WriteAllText(
                path, JsonSerializer.Serialize(
                    all, BrowseFilterJson.Default.DictionaryStringRememberedFilters));
        }
        catch (Exception refusal) when (refusal is IOException
                                            or UnauthorizedAccessException
                                            or NotSupportedException)
        {
            // A FULL DISK IS NOT WORTH A CONSOLE. What is lost is that the next
            // session starts from no filter, which is where every session
            // started before this existed.
        }
    }

    /// <summary>Nothing remembered: every answer that is not a readable file.</summary>
    private static readonly Dictionary<string, RememberedFilters> Nothing =
        new(StringComparer.Ordinal);

    /// <summary>Every tracker's filter, or an empty map.</summary>
    private static IReadOnlyDictionary<string, RememberedFilters> All(string? stateHome)
    {
        try
        {
            var path = PathFor(stateHome);

            return File.Exists(path)
                ? JsonSerializer.Deserialize(
                      File.ReadAllText(path),
                      BrowseFilterJson.Default.DictionaryStringRememberedFilters)
                  ?? Nothing
                : Nothing;
        }
        catch (Exception refusal) when (refusal is IOException
                                            or UnauthorizedAccessException
                                            or JsonException
                                            or NotSupportedException)
        {
            // Broken, unreadable, or written by something else. "No filter" is
            // the honest answer and the console opens either way.
            return Nothing;
        }
    }
}
