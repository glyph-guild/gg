namespace Gg.Console;

/// <summary>
/// Lines a watched runner has sent, waiting to be drawn.
/// </summary>
/// <remarks>
/// <para>
/// <b>A buffer, and deliberately nothing else.</b> This is read from inside a
/// UI session, and the rule that lets a session read at all is scoped to a
/// local file — because everything else in this console happens between
/// sessions with the terminal provably free. Draining memory is the same shape
/// as reading a file: no call, no wait, no way to block the screen.
/// </para>
/// <para>
/// <b>What fills it is somewhere else on purpose.</b> The pump holds the
/// conversation and asks the runner for its tail; it never touches the terminal
/// and never touches the model. Putting both halves in one class would leave a
/// network call one field away from a session — and the scan that guards this
/// reads a NAMED list of files, so a class that did both and was not on that
/// list would pass by not being looked at rather than by being right.
/// </para>
/// <para>
/// <b>Bounded, because a person watching is not a person archiving.</b> What
/// arrives between two ticks is small; what is unbounded is a console left open
/// on a long flight. An unread buffer that grows forever is a leak with a pane
/// in front of it, so the oldest go — they are the ones already on the screen.
/// </para>
/// </remarks>
public sealed class RemoteLiveSource(int keep = 2000) : ILiveSource
{
    private readonly Queue<StreamLine> _waiting = new();
    private readonly Lock _gate = new();
    private bool _open;

    /// <summary>
    /// Whether there is a channel to have heard anything over.
    /// </summary>
    /// <remarks>
    /// <b>The difference between two silences</b>, which is what this member is
    /// for on the file side too. No channel is "not started"; an open one with
    /// nothing new is "the agent is working and has not spoken". A pane showing
    /// the same box for both cannot tell a person which they are looking at.
    /// </remarks>
    public bool Exists
    {
        get
        {
            lock (_gate)
            {
                return _open;
            }
        }
    }

    /// <summary>Says a channel is carrying this flight now.</summary>
    public void Opened()
    {
        lock (_gate)
        {
            _open = true;
        }
    }

    /// <summary>Says it is not any more.</summary>
    public void Closed()
    {
        lock (_gate)
        {
            _open = false;
        }
    }

    /// <summary>Takes what arrived, from whoever is listening.</summary>
    /// <remarks>
    /// <b>Locked because the two ends are different threads.</b> The pump adds
    /// and the session drains, and they are not coordinated by anything else —
    /// which is the whole point: the session must never wait for the network.
    /// </remarks>
    public void Offer(IReadOnlyList<StreamLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        lock (_gate)
        {
            foreach (var line in lines)
            {
                _waiting.Enqueue(line);
            }

            while (_waiting.Count > keep)
            {
                _waiting.Dequeue();
            }
        }
    }

    /// <summary>Lines since the last call. Empty when there are none.</summary>
    public IReadOnlyList<StreamLine> Read()
    {
        lock (_gate)
        {
            if (_waiting.Count == 0)
            {
                return [];
            }

            var taken = _waiting.ToArray();
            _waiting.Clear();

            return taken;
        }
    }
}
