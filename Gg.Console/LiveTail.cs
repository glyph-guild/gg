using System.Text.Json;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// Reads a runner's live file, from wherever it last stopped.
/// </summary>
/// <remarks>
/// <para>
/// <b>A file, because the console is not the run.</b> ADR-0007 case 1: same
/// machine, no relay. The console can be closed and reopened and the flight
/// neither knows nor cares - leases outlive clients, and a transport that died
/// with the reader would have taken the run's output with it. Reopening picks up
/// from the offset it held, or from the start if it held none.
/// </para>
/// <para>
/// <b>A view, not evidence.</b> Nothing here is stored, hashed, or shipped. The
/// transcript is the reference and the digest is what crosses; this exists so a
/// person can watch, and it is deliberately a different type in a different file
/// from anything that travels.
/// </para>
/// <para>
/// <b>Partial lines are not consumed.</b> The runner appends while this reads,
/// so the last line can be half-written. It is left in place and re-read next
/// time rather than parsed into a line with a hole in it.
/// </para>
/// </remarks>
public sealed class LiveTail(string path) : ILiveSource
{
    private readonly string _path = path;

    /// <summary>Where reading stopped, so reopening does not replay everything.</summary>
    public long Offset { get; private set; }

    /// <summary>Whether this flight has a live view at all.</summary>
    public bool Exists => File.Exists(_path);

    /// <summary>
    /// What has arrived since last time.
    /// </summary>
    /// <remarks>
    /// An absent file is an empty read rather than an error: the pane is off by
    /// default and most flights never write one, so "nothing there" is the
    /// ordinary case and not a degradation.
    /// </remarks>
    public IReadOnlyList<StreamLine> Read()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        using var stream = new FileStream(
            _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (Offset > stream.Length)
        {
            // The file got shorter, so it is a different run in the same place.
            // Starting over beats reading from an offset into somebody else's
            // output.
            Offset = 0;
        }

        stream.Seek(Offset, SeekOrigin.Begin);

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        // Only whole lines. A trailing fragment is the runner mid-append.
        var lastBreak = text.LastIndexOf('\n');
        if (lastBreak < 0)
        {
            return [];
        }

        var complete = text[..lastBreak];
        Offset += System.Text.Encoding.UTF8.GetByteCount(text[..(lastBreak + 1)]);

        var lines = new List<StreamLine>();
        foreach (var line in complete.Split('\n'))
        {
            if (Parse(line) is { } parsed)
            {
                lines.Add(parsed);
            }
        }

        return lines;
    }

    /// <summary>
    /// One line, or null when it is not one.
    /// </summary>
    /// <remarks>
    /// Stripped again here even though the runner strips on the way in. Not
    /// belt-and-braces: this file is on disk and anything on this machine can
    /// write to it, so the console treats it as external text - which is the
    /// rule for external text everywhere else.
    /// </remarks>
    private static StreamLine? Parse(string line)
    {
        if (line.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("text", out var text)
                || text.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return new StreamLine
            {
                Kind = Kind(root),
                Text = ControlText.Strip(text.GetString() ?? "") ?? "",
                At = root.TryGetProperty("at", out var at)
                  && at.TryGetDateTimeOffset(out var stamp)
                    ? stamp
                    : DateTimeOffset.MinValue,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The kind, with anything unrecognised passed through as raw.
    /// </summary>
    /// <remarks>
    /// A kind this console does not know is still output somebody wants to see.
    /// Refusing it would hide a line because a newer runner named it something
    /// new, which is the wrong way round: the console is the older half here.
    /// </remarks>
    private static StreamLineKind Kind(JsonElement root) =>
        (root.TryGetProperty("kind", out var kind) ? kind.GetString() : null) switch
        {
            "text" => StreamLineKind.Text,
            "tool" => StreamLineKind.Tool,
            "meta" => StreamLineKind.Meta,
            "setup" => StreamLineKind.Setup,
            _ => StreamLineKind.Raw,
        };
}
