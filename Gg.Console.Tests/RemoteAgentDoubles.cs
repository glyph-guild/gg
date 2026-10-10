using System.Collections.Concurrent;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A data channel that is a list: what the console sent, and a way to say what the
/// machine sends back. Optionally answers like a runner would.
/// </summary>
internal sealed class FakeLink : IAgentLink
{
    internal ConcurrentQueue<AgentFrame> Sent { get; } = new();

    /// <summary>What the machine answers each frame with, if anything.</summary>
    internal Func<AgentFrame, IEnumerable<AgentFrame>>? Answers { get; init; }

    internal bool Disposed { get; private set; }

    public event Action<AgentFrame>? Heard;

    public event Action? Closed;

    public void Send(AgentFrame frame)
    {
        Sent.Enqueue(frame);

        foreach (var answer in Answers?.Invoke(frame) ?? [])
        {
            Heard?.Invoke(answer);
        }
    }

    /// <summary>The machine sends this.</summary>
    internal void Hear(AgentFrame frame) => Heard?.Invoke(frame);

    internal void Hear(string output) =>
        Heard?.Invoke(new AgentOutput { Bytes = System.Text.Encoding.UTF8.GetBytes(output) });

    /// <summary>The channel drops.</summary>
    internal void Close() => Closed?.Invoke();

    public void Dispose() => Disposed = true;

    /// <summary>A machine holding these sessions, starting or attaching as asked.</summary>
    internal static FakeLink Machine(params AgentSessionStanding[] held) => new()
    {
        Answers = frame => frame switch
        {
            ListAgentSessions => [new AgentSessionList { Sessions = held }],
            StartAgentSession start => [new AgentSessionStarted { SessionId = start.SessionId ?? "fresh" }],
            AttachAgentSession attach => [new AgentSessionStarted { SessionId = attach.SessionId }],
            _ => [],
        },
    };
}
