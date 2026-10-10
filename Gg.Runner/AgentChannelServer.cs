using System.Threading.Channels;
using Gg.Contracts;
using SIPSorcery.Net;

namespace Gg.Runner;

/// <summary>
/// Serves an `agent` channel: frames in become a session's input, a session's
/// output becomes frames out (slice seventy, ADR-0039 Decision 6).
/// </summary>
/// <remarks>
/// <para>
/// <b>One ordered queue out, per channel.</b> Output, the end of the session and
/// "you are watching now" all go through it, so a console never hears that the
/// agent exited before it has seen the last thing the agent wrote.
/// </para>
/// <para>
/// <b>Backpressure by polling, because there is nothing else.</b> SIPSorcery has
/// no buffered-amount-low event, so the sender waits while
/// <c>bufferedAmount</c> is over <see cref="PauseAbove"/>. A console so far
/// behind that its queue passes <see cref="CatchUpAbove"/> is not sent the
/// backlog at all: the queue is dropped and the agent is asked to repaint, which
/// is the screen as it is rather than everything it once was.
/// </para>
/// </remarks>
public sealed class AgentChannelServer(AgentSessions sessions)
{
    /// <summary>Wait to send while the channel holds more than this unsent.</summary>
    public const ulong PauseAbove = 1024 * 1024;

    /// <summary>Drop the backlog and repaint instead, past this much queued for one console.</summary>
    public const long CatchUpAbove = 4 * 1024 * 1024;

    /// <summary>What a machine that did not opt in answers an `agent` channel with.</summary>
    public const string NotOptedIn =
        "This machine's configuration does not say `accept-agent-sessions`, so it runs no agent "
      + "session for anybody. Its owner turns that on in its gg configuration.";

    /// <summary>Serve this channel until it closes.</summary>
    /// <param name="heard">Called on every frame either way, so the channel is not let go as quiet.</param>
    public void Serve(RTCDataChannel channel, Action heard)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(heard);

        var conversation = new Conversation(sessions, channel, heard);

        channel.onmessage += (_, _, data) => conversation.Heard(data);
        channel.onclose += conversation.Close;
    }

    /// <summary>Answer every frame on this channel with <see cref="NotOptedIn"/>.</summary>
    public static void Refuse(RTCDataChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        channel.onmessage += (_, _, _) =>
        {
            try
            {
                channel.send(AgentFrameCodec.Encode(new AgentSessionRefused { Because = NotOptedIn }));
            }
            catch (Exception gone) when (gone is InvalidOperationException or ObjectDisposedException)
            {
            }
        };
    }

    private sealed class Conversation : IAgentViewer
    {
        private readonly AgentSessions _sessions;
        private readonly RTCDataChannel _channel;
        private readonly Action _heard;
        private readonly Channel<AgentFrame> _out = Channel.CreateUnbounded<AgentFrame>(
            new UnboundedChannelOptions { SingleReader = true });
        private readonly Lock _gate = new();

        private AgentSession? _session;
        private IDisposable? _attached;
        private long _queued;

        public Conversation(AgentSessions sessions, RTCDataChannel channel, Action heard)
        {
            _sessions = sessions;
            _channel = channel;
            _heard = heard;
            _ = Task.Run(SendAsync);
        }

        public void Heard(byte[] data)
        {
            _heard();

            switch (AgentFrameCodec.Decode(data))
            {
                case StartAgentSession start:
                    _ = Task.Run(async () =>
                    {
                        var opened = await _sessions.StartAsync(start, CancellationToken.None);
                        Join(opened, start.Columns, start.Rows);
                    });
                    break;

                case AttachAgentSession attach:
                    Join(_sessions.Find(attach.SessionId), attach.Columns, attach.Rows);
                    break;

                case AgentInput input:
                    Current()?.Input(this, input.Bytes);
                    break;

                case AgentResize resize:
                    Current()?.Resize(this, resize.Columns, resize.Rows);
                    break;

                case KillAgentSession:
                    Current()?.Kill();
                    break;

                case ListAgentSessions:
                    Enqueue(new AgentSessionList { Sessions = _sessions.Standings() });
                    break;

                // FORGOTTEN, THEN LISTED: the console redraws from what the machine holds
                // after, or shows the machine's sentence for why not (slice seventy-one).
                case ForgetAgentSession forget:
                    Enqueue(_sessions.Forget(forget.SessionId) is { } why
                        ? new AgentSessionRefused { Because = why }
                        : new AgentSessionList { Sessions = _sessions.Standings() });
                    break;

                // A FRAME THIS SIDE CANNOT READ, or one only the runner sends, is
                // nothing - the ask channel's rule for what it does not know.
                default:
                    break;
            }
        }

        public void Close()
        {
            IDisposable? attached;

            lock (_gate)
            {
                attached = _attached;
                _attached = null;
                _session = null;
            }

            attached?.Dispose();
            _out.Writer.TryComplete();
        }

        public void Output(ReadOnlySpan<byte> bytes)
        {
            foreach (var chunk in AgentFrameCodec.Output(bytes.ToArray()))
            {
                Enqueue(chunk);
            }
        }

        public void Exited(int code) => Enqueue(new AgentSessionExited { Code = code });

        public void ReadOnly() => Enqueue(new AgentSessionReadOnly());

        private AgentSession? Current()
        {
            lock (_gate)
            {
                return _session;
            }
        }

        private void Join(AgentSessions.Opened opened, int columns, int rows)
        {
            if (opened.Session is not { } session)
            {
                Enqueue(new AgentSessionRefused { Because = opened.Refused ?? "refused" });
                return;
            }

            IDisposable? previous;

            lock (_gate)
            {
                previous = _attached;
                _session = session;
            }

            previous?.Dispose();

            // STARTED BEFORE THE REPLAY, so the console knows which session the
            // bytes that follow are.
            Enqueue(new AgentSessionStarted { SessionId = session.Id });
            var attached = session.Attach(this, columns, rows);

            lock (_gate)
            {
                _attached = attached;
            }
        }

        private void Enqueue(AgentFrame frame)
        {
            if (frame is AgentOutput output)
            {
                Interlocked.Add(ref _queued, output.Bytes.Length);
            }

            _out.Writer.TryWrite(frame);
        }

        private async Task SendAsync()
        {
            await foreach (var frame in _out.Reader.ReadAllAsync())
            {
                if (frame is AgentOutput output)
                {
                    var left = Interlocked.Add(ref _queued, -output.Bytes.Length);

                    // TOO FAR BEHIND TO BE WORTH CATCHING UP BYTE BY BYTE.
                    if (left + output.Bytes.Length > CatchUpAbove)
                    {
                        while (_out.Reader.TryPeek(out var next) && next is AgentOutput dropped)
                        {
                            _out.Reader.TryRead(out _);
                            Interlocked.Add(ref _queued, -dropped.Bytes.Length);
                        }

                        Current()?.Redraw();
                        continue;
                    }
                }

                try
                {
                    while (_channel.bufferedAmount > PauseAbove
                           && _channel.readyState == RTCDataChannelState.open)
                    {
                        await Task.Delay(5);
                    }

                    if (_channel.readyState != RTCDataChannelState.open)
                    {
                        continue;
                    }

                    _channel.send(AgentFrameCodec.Encode(frame));
                    _heard();
                }
                catch (Exception gone) when (gone is InvalidOperationException
                                                 or ObjectDisposedException
                                                 or IOException)
                {
                    // THE CONSOLE WENT AWAY UNDER A SEND. The session does not care.
                }
            }
        }
    }
}
