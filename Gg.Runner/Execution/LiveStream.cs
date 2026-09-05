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

    /// <summary>
    /// How large one flight's live view may get before the oldest of it goes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Half a megabyte, and the number is reasoned rather than measured -
    /// which is worth saying, because the criterion asked for a measurement.</b>
    /// The step-0 walk wrote 5,331 bytes in 51 seconds, and that is NOT a rate:
    /// eighteen of its thirty-seven lines were <c>setup</c>, a fixed cost paid
    /// once when a session starts. A flight running an hour does not write four
    /// hundred kilobytes; it writes the same setup plus whatever the agent
    /// actually says. Extrapolating from one short run would have produced a
    /// confident number with nothing behind it.
    /// </para>
    /// <para>
    /// So: half a megabyte is roughly a hundred times the only real flight
    /// measured, which no ordinary run will reach, and small enough that a
    /// pathological one cannot fill a disk. A longer walk should replace this
    /// with an observation; until then the reasoning is here rather than the
    /// number being presented as fact.
    /// </para>
    /// </remarks>
    private const long MaxFile = 512 * 1024;

    /// <summary>
    /// How much is kept when the cap is reached.
    /// </summary>
    /// <remarks>
    /// The NEWEST half, because peeking is about now. Keeping the oldest would
    /// mean a long run's pane freezes at whatever it was saying an hour ago,
    /// which is the opposite of what a live view is for.
    /// </remarks>
    private const long KeepOnRoll = MaxFile / 2;

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
            Roll();

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
    /// <summary>
    /// Drops the oldest half when the view has grown past its cap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rolling rather than stopping, because a capped file that stops growing
    /// is a pane that goes silent while the flight is still talking.</b> A
    /// person attaching to a long run needs the last few minutes, not the first
    /// few.
    /// </para>
    /// <para>
    /// <b>What a reader sees, said plainly: the retained lines again.</b>
    /// <c>LiveTail</c> restarts when the file gets shorter - it reads that as "a
    /// different run in the same place", which is the right reading for the case
    /// it was written for - so after a roll it re-reads what was kept. The
    /// console's own list is capped, so the duplication is bounded, and the
    /// alternative was a protocol between two halves that deliberately share
    /// nothing but a file. It happens once per half-megabyte.
    /// </para>
    /// <para>
    /// Cut at a line boundary, or the reader's refusal of partial lines would
    /// silently drop the first line after every roll.
    /// </para>
    /// </remarks>
    private void Roll()
    {
        var file = new FileInfo(_path);
        if (!file.Exists || file.Length <= MaxFile)
        {
            return;
        }

        var kept = File.ReadAllBytes(_path)[^(int)KeepOnRoll..];
        var firstBreak = Array.IndexOf(kept, (byte)'\n');

        File.WriteAllBytes(
            _path, firstBreak < 0 ? [] : kept[(firstBreak + 1)..]);
    }

    private static string Short(string text)
    {
        var clean = (ControlText.Strip(text) ?? "").ReplaceLineEndings(" ").Trim();

        return clean.Length <= MaxLine ? clean : clean[..MaxLine] + "…";
    }
}
