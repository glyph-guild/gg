namespace Gg.Console;

/// <summary>
/// The one runner this console is watching, and the buffer its output lands in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pump is the client's, not a second copy of it.</b> Following a runner
/// is already "ask again and write only what is new", and the thing it writes
/// to is a delegate — so this points that at a buffer and adds nothing. A
/// second implementation of the polling would be a second answer to how often a
/// runner is asked, and the two would drift.
/// </para>
/// <para>
/// <b>One at a time, deliberately.</b> There is one live pane. Two
/// conversations feeding it would interleave two flights into one box with
/// nothing saying which line came from where, and the runner nobody could see
/// any more would go on being asked once a second.
/// </para>
/// <para>
/// <b>This half holds the conversation; the buffer is the half a session
/// touches.</b> They are separate files so that the split is something a scan
/// can check rather than something a comment claims — see
/// <see cref="RemoteLiveSource"/>, which may name no conversation, and this,
/// which may name no view.
/// </para>
/// <para>
/// <b>It runs beside a session, and that is the new thing.</b> Everything else
/// in this console happens between sessions with the terminal free. This does
/// not touch the terminal and does not touch the model: it fills a queue behind
/// a lock, and the session drains it on the tick it already has. The rule that
/// matters — that exactly one thing owns the terminal — is untouched.
/// </para>
/// </remarks>
public sealed class WatchedRunner(
    Func<string, Action<string>, Action<string>, CancellationToken, Task> follow,
    Func<DateTimeOffset> now) : IDisposable
{
    private readonly Lock _gate = new();
    private string? _flightId;
    private RemoteLiveSource? _source;
    private CancellationTokenSource? _stopping;

    /// <summary>The buffer for a flight, or null when it is not the watched one.</summary>
    /// <remarks>
    /// <b>Null rather than an empty buffer</b>, because the composition root
    /// falls back to the file for anything this does not answer for — and an
    /// empty remote buffer in front of a local tail with lines in it is a pane
    /// that went blank for a flight it could see perfectly well.
    /// </remarks>
    public ILiveSource? SourceFor(string flightId)
    {
        lock (_gate)
        {
            return string.Equals(_flightId, flightId, StringComparison.Ordinal) ? _source : null;
        }
    }

    /// <summary>Starts watching one runner's flight, stopping whatever was.</summary>
    public void Start(string runnerId, string flightId)
    {
        Stop();

        var source = new RemoteLiveSource();
        source.Opened();

        var stopping = new CancellationTokenSource();

        lock (_gate)
        {
            _flightId = flightId;
            _source = source;
            _stopping = stopping;
        }

        // NOT AWAITED, and nothing may throw out of it. It runs beside a
        // session a person is using; an exception escaping here would take the
        // console down over a tail.
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await follow(
                        runnerId,
                        line => source.Offer(
                            [new StreamLine
                            {
                                Kind = StreamLineKind.Text,
                                Text = line,
                                At = now(),
                            }]),
                        // THE CONNECT'S OWN STEPS, dropped here: by the time
                        // this pump is running the connect has already
                        // happened, on the freed terminal, where a person read
                        // them. Kept in the signature because the caller that
                        // does the connecting is the one that supplies this.
                        _ => { },
                        stopping.Token);
                }
                catch (Exception)
                {
                    // SAID BY GOING QUIET, which the pane can already tell from
                    // an open channel with nothing on it: the source closes, so
                    // the box says the watch ended rather than that the agent
                    // is thinking.
                }
                finally
                {
                    source.Closed();

                    // DISPOSED HERE, BY WHOEVER USED IT. Stop() used to cancel
                    // and dispose in one breath, which is a race when the pump
                    // has not started yet: it then hands an already-disposed
                    // token to `follow`, and registering on one throws. The
                    // watch died before it began and the pane said nothing,
                    // because the catch above is deliberately silent.
                    stopping.Dispose();
                }
            },
            CancellationToken.None);
    }

    /// <summary>Ends the watch, and says so on the pane.</summary>
    public void Stop()
    {
        CancellationTokenSource? stopping;
        RemoteLiveSource? source;

        lock (_gate)
        {
            stopping = _stopping;
            source = _source;
            _stopping = null;
            _source = null;
            _flightId = null;
        }

        // CLOSED FIRST, so a pane drawn between these two lines says the watch
        // ended rather than that the channel is open with nothing on it.
        source?.Closed();

        // CANCELLED, NOT DISPOSED. The pump owns the disposal, because it is
        // the thing that might still be holding the token.
        stopping?.Cancel();
    }

    public void Dispose() => Stop();
}
