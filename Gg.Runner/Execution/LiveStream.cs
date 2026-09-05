using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>What kind of line this is. The console's five, named once.</summary>
/// <remarks>
/// Typed rather than parsed, so verbosity is a data model instead of a regular
/// expression applied to a screen. The console has carried these since 4b with
/// nothing producing them; this is what produces them.
/// </remarks>
public static class LiveLineKinds
{
    /// <summary>What the agent said.</summary>
    public const string Text = "text";

    /// <summary>A tool call and its result.</summary>
    public const string Tool = "tool";

    /// <summary>Unclassified output, passed through.</summary>
    public const string Raw = "raw";

    /// <summary>Our own narration about the run.</summary>
    public const string Meta = "meta";

    /// <summary>Environment preparation, before any work.</summary>
    public const string Setup = "setup";

    public static IReadOnlyList<string> All { get; } = [Text, Tool, Raw, Meta, Setup];
}

/// <summary>One line as it travels from runner to console.</summary>
public sealed record LiveLine
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("at")]
    public required DateTimeOffset At { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LiveLine))]
internal sealed partial class LiveJson : JsonSerializerContext;

/// <summary>
/// The live view's transport: a local file the runner appends and the console
/// tails.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0007 case 1 only.</b> Same machine, no relay, no same-network. A file
/// needs no port, no discovery and no protocol version, and it survives the
/// console being closed and reopened - which is the realistic case, because
/// leases outlive clients. A socket would have died with the reader and taken
/// the run's output with it.
/// </para>
/// <para>
/// <b>This is not evidence.</b> It is ephemeral, local, and crosses nothing. The
/// transcript is the <c>reference</c> and the digest is what travels; this is a
/// view, and keeping it in its own file with its own type is what makes "nothing
/// from the live channel is ever in a bundle" a thing you can check rather than
/// a thing you hope.
/// </para>
/// <para>
/// <b>Stripped on the way in.</b> Agent output is somebody else's text and can
/// carry escape sequences; the console renders this, and a console that renders
/// an escape is a console being driven. Stripped here, in the runner, for the
/// same reason the digest is: at ingress, before storage, never at render time.
/// </para>
/// </remarks>
public sealed class LiveStream
{
    /// <summary>How much of one line is worth carrying to a screen.</summary>
    private const int MaxLine = 2000;

    private readonly string _path;
    private readonly TimeProvider _time;

    public LiveStream(string path, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Where a flight's live view lives when nobody overrides it.</summary>
    /// <remarks>
    /// Under the transcript root but in its own directory, because these are
    /// deletable and transcripts are not. Somebody clearing live views must not
    /// have to be careful about which files they are.
    /// </remarks>
    public static string DefaultPath(string flightId, string? root = null) =>
        Gg.Local.LocalPaths.LiveView(flightId, root);

    /// <summary>Appends one line, and never throws into the run.</summary>
    /// <remarks>
    /// <b>A view that fails must not fail the flight.</b> A full disk, a
    /// directory somebody removed, a permission change - none of those are
    /// reasons to lose the work. The line is dropped and the run continues,
    /// which is the one place in this system where dropping something silently
    /// is the correct answer: nothing downstream treats this as a record.
    /// </remarks>
    public void Append(string kind, string text)
    {
        try
        {
            var line = new LiveLine
            {
                Kind = kind,
                Text = Short(text),
                At = _time.GetUtcNow(),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            // Opened and closed per line, so a console tailing this sees each
            // one as it lands rather than when a buffer happens to flush.
            using var stream = new FileStream(
                _path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));

            writer.WriteLine(JsonSerializer.Serialize(line, LiveJson.Default.LiveLine));
        }
        catch (IOException)
        {
            // See the remark. Deliberately swallowed.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Stripped, then cut.</summary>
    private static string Short(string text)
    {
        var clean = (ControlText.Strip(text) ?? "").ReplaceLineEndings(" ").Trim();

        return clean.Length <= MaxLine ? clean : clean[..MaxLine] + "…";
    }
}
