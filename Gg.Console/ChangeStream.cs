using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What the control plane says has changed, heard on a stream beside the
/// console and folded on its tick.
/// </summary>
public sealed class ChangeStream(
    Func<CancellationToken, IAsyncEnumerable<ChangeNotice>> listen,
    Expectations? expectations = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private readonly Func<CancellationToken, IAsyncEnumerable<ChangeNotice>> _listen = listen;
    private readonly Expectations? _expectations = expectations;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay =
        delay ?? ((span, ct) => Task.Delay(span, ct));

    /// <summary>Whether a connection is open and has said it is ready.</summary>
    public bool Live { get; private set; }

    /// <summary>Whether this control plane serves no stream at all.</summary>
    public bool Absent { get; private set; }

    /// <summary>Starts listening, once, on a task of its own.</summary>
    public void Start()
    {
    }

    /// <summary>Connections, one after another, until there is no stream to have.</summary>
    /// <remarks>
    /// Public with <see cref="ListenOnceAsync"/> so each can be driven a step at
    /// a time; <see cref="Start"/> is what the console calls.
    /// </remarks>
    public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>One connection, heard until it ends.</summary>
    public Task ListenOnceAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Folds what has been heard since the last tick.</summary>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _ = _listen;
        _ = _expectations;
        _ = _delay;
        return state;
    }
}
