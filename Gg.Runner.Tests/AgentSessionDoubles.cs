using System.Collections.Concurrent;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>A child that is a pair of queues: what it writes, what it was sent.</summary>
internal sealed class FakeAgentChild : IAgentSessionChild
{
    private readonly BlockingCollection<byte[]> _out = [];
    private byte[]? _left;
    private readonly TaskCompletionSource<int> _exited =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal List<byte> Typed { get; } = [];

    internal List<(int Columns, int Rows)> Resized { get; } = [];

    internal bool Killed { get; private set; }

    /// <summary>The child writes this to its terminal.</summary>
    internal void Say(string text) => _out.Add(System.Text.Encoding.UTF8.GetBytes(text));

    internal void Say(byte[] bytes) => _out.Add(bytes);

    /// <summary>The child ends with this code.</summary>
    internal void End(int code)
    {
        _out.CompleteAdding();
        _exited.TrySetResult(code);
    }

    public int Read(byte[] buffer)
    {
        try
        {
            // A REAL TERMINAL HANDS OVER WHAT FITS, and the rest on the next read.
            var next = _left ?? _out.Take();
            var taken = Math.Min(next.Length, buffer.Length);
            next.AsSpan(0, taken).CopyTo(buffer);
            _left = taken < next.Length ? next[taken..] : null;
            return taken;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        lock (Typed)
        {
            Typed.AddRange(bytes.ToArray());
        }
    }

    public void Resize(int columns, int rows)
    {
        lock (Resized)
        {
            Resized.Add((columns, rows));
        }
    }

    public void Kill()
    {
        Killed = true;
        End(137);
    }

    public Task<int> Exited => _exited.Task;

    public void Dispose()
    {
    }
}

/// <summary>A host that hands out fake children and remembers how each was asked for.</summary>
internal sealed class FakeAgentHost : IHostAgentSessions
{
    internal List<AgentSessionStart> Started { get; } = [];

    internal List<FakeAgentChild> Children { get; } = [];

    public Task<IAgentSessionChild> StartAsync(AgentSessionStart start, CancellationToken cancellationToken)
    {
        var child = new FakeAgentChild();

        lock (Started)
        {
            Started.Add(start);
            Children.Add(child);
        }

        return Task.FromResult<IAgentSessionChild>(child);
    }
}

/// <summary>A console's end of a session: what it was shown and told.</summary>
internal sealed class FakeViewer : IAgentViewer
{
    private readonly List<byte> _seen = [];

    internal bool WasMadeReadOnly { get; private set; }

    internal int? ExitedWith { get; private set; }

    internal string Seen
    {
        get
        {
            lock (_seen)
            {
                return System.Text.Encoding.UTF8.GetString([.. _seen]);
            }
        }
    }

    internal int SeenBytes
    {
        get
        {
            lock (_seen)
            {
                return _seen.Count;
            }
        }
    }

    public void Output(ReadOnlySpan<byte> bytes)
    {
        lock (_seen)
        {
            _seen.AddRange(bytes.ToArray());
        }
    }

    public void Exited(int code) => ExitedWith = code;

    public void ReadOnly() => WasMadeReadOnly = true;
}

/// <summary>Bounded patience for a background pump, never a sleep-then-assert.</summary>
internal static class Eventually
{
    internal static async Task<bool> TrueAsync(Func<bool> condition, int milliseconds = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Yield();
        }

        return condition();
    }
}
