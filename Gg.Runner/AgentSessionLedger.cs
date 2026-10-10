using System.Text.Json;

namespace Gg.Runner;

/// <summary>
/// The sessions a machine has held, on its own disk (slice seventy-one, ADR-0039 Decision 8),
/// and the file operations a session's directory needs.
/// </summary>
/// <remarks>
/// <b>Read and written by hand</b>, with <see cref="Utf8JsonWriter"/> and
/// <see cref="JsonDocument"/>: AOT-safe without a serializer context for four fields. An
/// unreadable file is an empty ledger, not a runner that will not start.
/// </remarks>
public static class AgentSessionLedger
{
    /// <summary>The ledger's name, beside the session directories.</summary>
    public const string FileName = "sessions.json";

    /// <summary>One session, as the ledger keeps it.</summary>
    public sealed record Kept(string Id, string Directory, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

    /// <summary>Whether an id can name a directory: letters, digits, dash and underscore only.</summary>
    public static bool IsPlainName(string id) =>
        id.Length is > 0 and <= 128 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>
    /// The folder Claude keeps a directory's conversations in: the path with every character
    /// that is not a letter or a digit made a dash.
    /// </summary>
    public static string TranscriptName(string directory) =>
        new([.. directory.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);

    /// <summary>Makes the directory if it is missing, readable only by this user.</summary>
    public static void Private(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Deletes a directory and everything in it, when it is there.</summary>
    public static void Delete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception refused) when (refused is IOException or UnauthorizedAccessException)
        {
            // A FILE THE AGENT LEFT LOCKED OR UNWRITABLE is not a reason to keep the ledger
            // entry: the person asked for it gone, and what is left is left on disk only.
        }
    }

    public static IReadOnlyList<Kept> Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var kept = new List<Kept>();

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.TryGetProperty("id", out var id) && id.GetString() is { } named && IsPlainName(named)
                    && entry.TryGetProperty("directory", out var directory) && directory.GetString() is { } where
                    && entry.TryGetProperty("startedAt", out var started) && started.TryGetDateTimeOffset(out var at))
                {
                    DateTimeOffset? ended = entry.TryGetProperty("endedAt", out var end)
                                            && end.ValueKind == JsonValueKind.String
                                            && end.TryGetDateTimeOffset(out var endedAt)
                        ? endedAt
                        : null;
                    kept.Add(new Kept(named, where, at, ended));
                }
            }

            return kept;
        }
        catch (Exception unreadable) when (unreadable is IOException or JsonException or InvalidOperationException
                                               or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Writes the ledger whole, through a temporary file, so a crash leaves the old one.</summary>
    public static void Write(string path, IReadOnlyList<Kept> sessions)
    {
        var temporary = path + ".writing";

        using (var stream = File.Create(temporary))
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartArray();

            foreach (var session in sessions)
            {
                writer.WriteStartObject();
                writer.WriteString("id", session.Id);
                writer.WriteString("directory", session.Directory);
                writer.WriteString("startedAt", session.StartedAt);

                if (session.EndedAt is { } ended)
                {
                    writer.WriteString("endedAt", ended);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        File.Move(temporary, path, overwrite: true);
    }
}
