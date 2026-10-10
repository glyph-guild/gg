using System.Buffers.Binary;
using System.Text;

namespace Gg.Contracts;

/// <summary>
/// The `agent` data channel: an ad hoc Claude session on another machine,
/// carried as its terminal (slice seventy, ADR-0039).
/// </summary>
/// <remarks>
/// <para>
/// <b>Not the `tail` channel, and not a RunnerAsk.</b> That one is one question
/// and one bounded answer, and <c>RunnerAskIsNotStreamingTests</c> keeps it so.
/// A terminal is bytes both ways for as long as the session lives, so it is a
/// second channel the console creates on a <c>drive-an-agent</c> introduction,
/// with frames of its own.
/// </para>
/// <para>
/// <b>gg parses nothing of the conversation.</b> The far side is the
/// <c>claude</c> CLI as it is (Decision 2); what crosses is its terminal, and
/// the only structure here is gg's own: start, attach, resize, kill, and what
/// the machine says back.
/// </para>
/// </remarks>
public static class AgentChannel
{
    /// <summary>The label the console creates the channel with, and the runner serves.</summary>
    public const string Label = "agent";

    /// <summary>The most terminal bytes one frame carries.</summary>
    /// <remarks>
    /// <b>Far under SIPSorcery's 256 KiB message cap</b>, which it fixes and throws
    /// on synchronously - and small enough that one frame never holds up a
    /// keystroke behind it for long.
    /// </remarks>
    public const int MaxPayload = 16 * 1024;

    /// <summary>The largest encoded frame the codec produces from output or input.</summary>
    public const int MaxMessage = MaxPayload + 64;
}

/// <summary>Each frame's first byte. Console to runner below 64, runner to console above.</summary>
public static class AgentFrameKinds
{
    public const byte Start = 1;
    public const byte Attach = 2;
    public const byte Input = 3;
    public const byte Resize = 4;
    public const byte Kill = 5;
    public const byte List = 6;
    public const byte Forget = 7;

    public const byte Started = 65;
    public const byte Output = 66;
    public const byte Exited = 67;
    public const byte Sessions = 68;
    public const byte Refused = 69;
    public const byte ReadOnly = 70;
}

/// <summary>One frame on the `agent` channel.</summary>
[PinnedId("68771423-8192-4c8a-846f-daa88d48f641")]
public abstract record AgentFrame;

/// <summary>Start the machine's agent in a pty of this size, under a root it allows.</summary>
[PinnedId("f18dd2e5-9bd8-4f5b-87f0-b6a3de617b83")]
public sealed record StartAgentSession : AgentFrame
{
    public required int Columns { get; init; }

    public required int Rows { get; init; }

    /// <summary>Where to start it, which must be under one of the machine's roots; null is the first root.</summary>
    public string? Directory { get; init; }

    /// <summary>The console's id for it, so history can resume it; null lets the machine choose.</summary>
    public string? SessionId { get; init; }
}

/// <summary>Attach to a session the machine is already holding, at this size.</summary>
[PinnedId("b4057ae6-465c-4ae6-a711-3ead8c1c465a")]
public sealed record AttachAgentSession : AgentFrame
{
    public required string SessionId { get; init; }

    public required int Columns { get; init; }

    public required int Rows { get; init; }
}

/// <summary>Keystrokes for the session, from whoever drives it.</summary>
[PinnedId("2295f884-31d2-496a-bb88-60390cc1f8e3")]
public sealed record AgentInput : AgentFrame
{
    public required byte[] Bytes { get; init; }
}

/// <summary>The console's pane changed size.</summary>
[PinnedId("6651b907-60c9-44e4-a21c-c328000e9d90")]
public sealed record AgentResize : AgentFrame
{
    public required int Columns { get; init; }

    public required int Rows { get; init; }
}

/// <summary>End the attached session's agent.</summary>
[PinnedId("5e18696b-c349-45f4-8a9d-149612d42f6c")]
public sealed record KillAgentSession : AgentFrame;

/// <summary>Ask which sessions the machine holds.</summary>
[PinnedId("d1dc5452-b1aa-4430-9791-ae7f5a3d82b2")]
public sealed record ListAgentSessions : AgentFrame;

/// <summary>
/// Delete an ended session: its ledger entry, its directory and Claude's transcript of it
/// (slice seventy-one, ADR-0039 Decision 9). The machine answers with its sessions after, or
/// refuses a live one.
/// </summary>
[PinnedId("0ec70056-30d5-41b6-9cfb-7a2e715cce01")]
public sealed record ForgetAgentSession : AgentFrame
{
    /// <summary>The session to forget; null forgets every ended one.</summary>
    public string? SessionId { get; init; }
}

/// <summary>The session this channel is now attached to.</summary>
[PinnedId("5a2e6427-1ba8-426a-b083-fe6e28199da1")]
public sealed record AgentSessionStarted : AgentFrame
{
    public required string SessionId { get; init; }
}

/// <summary>What the agent's terminal wrote.</summary>
[PinnedId("b7be67c5-92db-453f-a964-448cee9135e2")]
public sealed record AgentOutput : AgentFrame
{
    public required byte[] Bytes { get; init; }
}

/// <summary>The agent ended, with this exit code.</summary>
[PinnedId("e3d20874-d393-4291-9bb6-f2002a45632d")]
public sealed record AgentSessionExited : AgentFrame
{
    public required int Code { get; init; }
}

/// <summary>The sessions the machine holds.</summary>
[PinnedId("b4231a00-2d57-4b34-ae3f-3a3b5ae6c81d")]
public sealed record AgentSessionList : AgentFrame
{
    public required IReadOnlyList<AgentSessionStanding> Sessions { get; init; }
}

/// <summary>Why the machine will not do what was asked, in a sentence.</summary>
[PinnedId("f42ec54e-7576-42d9-8f12-850561f6d14e")]
public sealed record AgentSessionRefused : AgentFrame
{
    public required string Because { get; init; }
}

/// <summary>Somebody attached after this channel did, and drives now; this one watches.</summary>
[PinnedId("a70a5765-ee82-4e2e-b6c2-d58c6fd38748")]
public sealed record AgentSessionReadOnly : AgentFrame;

/// <summary>One session a machine holds, as a frame lists it and a heartbeat reports it.</summary>
[PinnedId("5916503b-50a2-4bc4-b6fa-318d5e0c6267")]
public sealed record AgentSessionStanding
{
    public required string SessionId { get; init; }

    public string? Directory { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Who drives it now, by the display the introduction named; null when nobody is attached.</summary>
    public string? AttachedBy { get; init; }

    /// <summary>Whether its agent is still running; false is a session that ended and can be resumed.</summary>
    public required bool Alive { get; init; }

    /// <summary>When its agent ended; null while it runs.</summary>
    /// <remarks>
    /// <b>JSON only: the heartbeat and the fleet read carry it, the channel does not.</b>
    /// <see cref="AgentFrameCodec"/> reads a session list field by field and refuses bytes left
    /// over, so a field added there would make every console already installed read a newer
    /// machine's list as nothing.
    /// </remarks>
    public DateTimeOffset? EndedAt { get; init; }
}

/// <summary>
/// Frames to bytes and back: a kind byte, then the payload.
/// </summary>
/// <remarks>
/// <b>Hand-written, because it is small and must be AOT-safe</b>, and because a
/// terminal's bytes should cross as bytes rather than as base64 inside JSON.
/// Integers are big-endian int32; strings are an int32 length and UTF-8, with
/// -1 for null. <see cref="Decode"/> answers null for anything it cannot read in
/// full - a newer peer's kind, a truncated frame - and never throws.
/// </remarks>
public static class AgentFrameCodec
{
    public static byte[] Encode(AgentFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var writer = new Writer();

        switch (frame)
        {
            case StartAgentSession s:
                writer.Kind(AgentFrameKinds.Start).Int(s.Columns).Int(s.Rows).Text(s.Directory).Text(s.SessionId);
                break;
            case AttachAgentSession a:
                writer.Kind(AgentFrameKinds.Attach).Text(a.SessionId).Int(a.Columns).Int(a.Rows);
                break;
            case AgentInput i:
                writer.Kind(AgentFrameKinds.Input).Rest(i.Bytes);
                break;
            case AgentResize r:
                writer.Kind(AgentFrameKinds.Resize).Int(r.Columns).Int(r.Rows);
                break;
            case KillAgentSession:
                writer.Kind(AgentFrameKinds.Kill);
                break;
            case ListAgentSessions:
                writer.Kind(AgentFrameKinds.List);
                break;
            case ForgetAgentSession f:
                writer.Kind(AgentFrameKinds.Forget).Text(f.SessionId);
                break;
            case AgentSessionStarted s:
                writer.Kind(AgentFrameKinds.Started).Text(s.SessionId);
                break;
            case AgentOutput o:
                writer.Kind(AgentFrameKinds.Output).Rest(o.Bytes);
                break;
            case AgentSessionExited e:
                writer.Kind(AgentFrameKinds.Exited).Int(e.Code);
                break;
            case AgentSessionList l:
                writer.Kind(AgentFrameKinds.Sessions).Int(l.Sessions.Count);
                foreach (var session in l.Sessions)
                {
                    writer.Text(session.SessionId).Text(session.Directory)
                        .Long(session.StartedAt.ToUnixTimeMilliseconds())
                        .Text(session.AttachedBy).Int(session.Alive ? 1 : 0);
                }

                break;
            case AgentSessionRefused r:
                writer.Kind(AgentFrameKinds.Refused).Text(r.Because);
                break;
            case AgentSessionReadOnly:
                writer.Kind(AgentFrameKinds.ReadOnly);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(frame), frame.GetType().Name, "not an agent frame");
        }

        return writer.ToArray();
    }

    public static AgentFrame? Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        var reader = new Reader(bytes[1..]);

        AgentFrame? frame = bytes[0] switch
        {
            AgentFrameKinds.Start => reader.Int(out var c) && reader.Int(out var r)
                                     && reader.Text(out var d) && reader.Text(out var id)
                ? new StartAgentSession { Columns = c, Rows = r, Directory = d, SessionId = id }
                : null,
            AgentFrameKinds.Attach => reader.Text(out var id) && id is not null
                                      && reader.Int(out var c) && reader.Int(out var r)
                ? new AttachAgentSession { SessionId = id, Columns = c, Rows = r }
                : null,
            AgentFrameKinds.Input => new AgentInput { Bytes = reader.Rest() },
            AgentFrameKinds.Resize => reader.Int(out var c) && reader.Int(out var r)
                ? new AgentResize { Columns = c, Rows = r }
                : null,
            AgentFrameKinds.Kill => new KillAgentSession(),
            AgentFrameKinds.List => new ListAgentSessions(),
            AgentFrameKinds.Forget => reader.Text(out var forgotten) ? new ForgetAgentSession { SessionId = forgotten } : null,
            AgentFrameKinds.Started => reader.Text(out var id) && id is not null
                ? new AgentSessionStarted { SessionId = id }
                : null,
            AgentFrameKinds.Output => new AgentOutput { Bytes = reader.Rest() },
            AgentFrameKinds.Exited => reader.Int(out var code) ? new AgentSessionExited { Code = code } : null,
            AgentFrameKinds.Sessions => Sessions(ref reader),
            AgentFrameKinds.Refused => reader.Text(out var because) && because is not null
                ? new AgentSessionRefused { Because = because }
                : null,
            AgentFrameKinds.ReadOnly => new AgentSessionReadOnly(),
            _ => null,
        };

        // A FRAME WITH BYTES LEFT OVER is not one this side wrote, so it is not
        // one this side reads. The two frames whose payload is the rest of the
        // message consumed it all by definition.
        return frame is not null && reader.Done ? frame : null;
    }

    /// <summary>Terminal output, cut into frames no larger than <see cref="AgentChannel.MaxPayload"/>.</summary>
    public static IEnumerable<AgentOutput> Output(ReadOnlyMemory<byte> bytes)
    {
        for (var at = 0; at < bytes.Length; at += AgentChannel.MaxPayload)
        {
            yield return new AgentOutput
            {
                Bytes = bytes.Slice(at, Math.Min(AgentChannel.MaxPayload, bytes.Length - at)).ToArray(),
            };
        }
    }

    /// <summary>Keystrokes, cut the same way - a paste can be large.</summary>
    public static IEnumerable<AgentInput> Input(ReadOnlyMemory<byte> bytes)
    {
        for (var at = 0; at < bytes.Length; at += AgentChannel.MaxPayload)
        {
            yield return new AgentInput
            {
                Bytes = bytes.Slice(at, Math.Min(AgentChannel.MaxPayload, bytes.Length - at)).ToArray(),
            };
        }
    }

    private static AgentSessionList? Sessions(ref Reader reader)
    {
        if (!reader.Int(out var count) || count < 0 || count > 1024)
        {
            return null;
        }

        var sessions = new List<AgentSessionStanding>(count);

        for (var i = 0; i < count; i++)
        {
            if (!reader.Text(out var id) || id is null
                || !reader.Text(out var directory)
                || !reader.Long(out var started)
                || !reader.Text(out var by)
                || !reader.Int(out var alive))
            {
                return null;
            }

            sessions.Add(new AgentSessionStanding
            {
                SessionId = id,
                Directory = directory,
                StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(started),
                AttachedBy = by,
                Alive = alive != 0,
            });
        }

        return new AgentSessionList { Sessions = sessions };
    }

    private sealed class Writer
    {
        private readonly List<byte> _bytes = [];

        public Writer Kind(byte kind)
        {
            _bytes.Add(kind);
            return this;
        }

        public Writer Int(int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            _bytes.AddRange(buffer.ToArray());
            return this;
        }

        public Writer Long(long value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            _bytes.AddRange(buffer.ToArray());
            return this;
        }

        public Writer Text(string? value)
        {
            if (value is null)
            {
                return Int(-1);
            }

            var utf8 = Encoding.UTF8.GetBytes(value);
            Int(utf8.Length);
            _bytes.AddRange(utf8);
            return this;
        }

        public Writer Rest(byte[] value)
        {
            _bytes.AddRange(value);
            return this;
        }

        public byte[] ToArray() => [.. _bytes];
    }

    private ref struct Reader(ReadOnlySpan<byte> bytes)
    {
        private ReadOnlySpan<byte> _bytes = bytes;

        public readonly bool Done => _bytes.IsEmpty;

        public bool Int(out int value)
        {
            value = 0;

            if (_bytes.Length < 4)
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt32BigEndian(_bytes);
            _bytes = _bytes[4..];
            return true;
        }

        public bool Long(out long value)
        {
            value = 0;

            if (_bytes.Length < 8)
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt64BigEndian(_bytes);
            _bytes = _bytes[8..];
            return true;
        }

        public bool Text(out string? value)
        {
            value = null;

            if (!Int(out var length))
            {
                return false;
            }

            if (length == -1)
            {
                return true;
            }

            if (length < 0 || length > _bytes.Length)
            {
                return false;
            }

            value = Encoding.UTF8.GetString(_bytes[..length]);
            _bytes = _bytes[length..];
            return true;
        }

        public byte[] Rest()
        {
            var rest = _bytes.ToArray();
            _bytes = [];
            return rest;
        }
    }
}
