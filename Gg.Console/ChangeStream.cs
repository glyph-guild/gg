using System.Collections.Concurrent;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What the control plane says has changed, heard on a stream beside the
/// console and folded on its tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>A doorbell, not a feed.</b> A notice is a topic and an id. What changed is
/// read through the routes the console already reads - the tab in front of
/// somebody, the flight or gate it is looking for - so this adds no second
/// picture of anything; what it changes is WHEN the picture is taken.
/// </para>
/// <para>
/// <b><see cref="AutoRefresh"/>'s shape.</b> The connection runs on a task the
/// composition root owns, outside every UI lifetime; what it hears is queued,
/// and the session's tick folds the queue and never waits on the stream. It is
/// an HTTP request, not a child process, and nothing waits for it to open - a
/// console comes up exactly as it did and hears changes once it can.
/// </para>
/// <para>
/// <b>The poll is the backstop, not the fallback.</b> A dropped connection is
/// opened again after a wait that grows to a minute; a control plane that
/// serves no stream answers 404, which is the answer that there is none to
/// wait for - and in both, the thirty-second tick is still what it was.
/// </para>
/// </remarks>
public sealed class ChangeStream(
    Func<CancellationToken, IAsyncEnumerable<ChangeNotice>> listen,
    Expectations? expectations = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    /// <summary>The first wait before opening a dropped stream again.</summary>
    public static readonly TimeSpan FirstWait = TimeSpan.FromSeconds(1);

    /// <summary>The longest wait between two attempts.</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(1);

    private readonly Func<CancellationToken, IAsyncEnumerable<ChangeNotice>> _listen = listen;
    private readonly Expectations? _expectations = expectations;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay =
        delay ?? ((span, ct) => Task.Delay(span, ct));

    /// <summary>What has been heard and not yet folded - written by the stream, drained by the tick.</summary>
    private readonly ConcurrentQueue<ChangeNotice> _heard = new();

    private int _started;
    private volatile bool _live;
    private volatile bool _absent;

    /// <summary>Whether a connection is open and has said it is ready.</summary>
    public bool Live => _live;

    /// <summary>Whether this control plane serves no stream at all.</summary>
    public bool Absent => _absent;

    /// <summary>Starts listening, once, on a task of its own.</summary>
    /// <remarks>
    /// <b>Never throws out of that task.</b> It runs beside a console somebody is
    /// using, and an exception escaping it would end the process over a
    /// notification.
    /// </remarks>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _ = Task.Run(() => RunAsync(CancellationToken.None));
    }

    /// <summary>Connections, one after another, until there is no stream to have.</summary>
    /// <remarks>
    /// Public with <see cref="ListenOnceAsync"/> so each can be driven a step at
    /// a time; <see cref="Start"/> is what the console calls.
    /// </remarks>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var wait = FirstWait;

        while (!cancellationToken.IsCancellationRequested)
        {
            // A CONNECTION THAT GOT AS FAR AS READY WORKED, and the growth starts
            // again from the bottom: what grows is patience with one that keeps
            // failing, not with one that was up for an hour and dropped.
            if (await Listened(cancellationToken))
            {
                wait = FirstWait;
            }

            if (_absent)
            {
                return;
            }

            try
            {
                await _delay(wait, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            wait = wait + wait < LongestWait ? wait + wait : LongestWait;
        }
    }

    /// <summary>One connection, heard until it ends.</summary>
    public Task ListenOnceAsync(CancellationToken cancellationToken) => Listened(cancellationToken);

    /// <summary>
    /// Folds what has been heard since the last tick: the tab read now, anything
    /// looked for looked for now, and whether the key says "live".
    /// </summary>
    /// <remarks>
    /// <b>The same model back when nothing moved</b>, so the tick draws nothing it
    /// does not have to.
    /// </remarks>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var heard = false;

        while (_heard.TryDequeue(out _))
        {
            heard = true;
        }

        var live = _live;

        if (heard)
        {
            _expectations?.Hurry();
        }

        if (!heard && state.Refresh.Live == live)
        {
            return state;
        }

        return state with
        {
            Refresh = state.Refresh with
            {
                Live = live,
                Wanted = state.Refresh.Wanted || heard,
            },
        };
    }

    private async Task<bool> Listened(CancellationToken cancellationToken)
    {
        var ready = false;

        try
        {
            await foreach (var notice in _listen(cancellationToken).WithCancellation(cancellationToken))
            {
                if (string.Equals(notice.Topic, ChangeTopics.Ready, StringComparison.Ordinal))
                {
                    ready = true;
                    _live = true;
                }

                _heard.Enqueue(notice);
            }
        }
        catch (ChangeStreamUnavailableException)
        {
            // THE ANSWER THAT THERE IS NO STREAM. Asking again changes nothing,
            // so this console polls, as it did before there was one.
            _absent = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // A DROPPED CONNECTION, A SESSION THAT RAN OUT, A CONTROL PLANE
            // BETWEEN REVISIONS. None of them is this console's to report - the
            // poll is still running, and the next attempt is the answer.
        }
        finally
        {
            _live = false;
        }

        return ready;
    }
}
