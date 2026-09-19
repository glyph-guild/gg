namespace Gg.Contracts;

/// <summary>
/// Something a console shows has changed, and which pane it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>A doorbell, not a feed.</b> It names a topic and, where there is one, an
/// id - never the row. The console reads the row through the route that already
/// serves it, so what a console may see is decided in one place, by the route
/// that has always decided it. A notice carrying the row would be a second read
/// surface with its own idea of who may see what.
/// </para>
/// <para>
/// <b>Missing one costs a refresh interval.</b> A console still refreshes on its
/// timer, so a notice dropped by a proxy, a reconnect or a control plane
/// restarting is a pane that catches up a little later - not a pane that is
/// wrong until somebody presses a key.
/// </para>
/// <para>
/// <b>Sent once the change is readable.</b> A flight is projected a hop after it
/// is created, which is why the re-read after opening one missed it. The control
/// plane sends this after the projection's checkpoint is saved, so the read it
/// prompts cannot lose that race.
/// </para>
/// </remarks>
[PinnedId("9aa9c1aa-cbfe-4c88-97f1-da53e87e5627")]
public sealed record ChangeNotice
{
    /// <summary>One of <see cref="ChangeTopics.All"/>.</summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Which one moved - a flight id, a runner id, a nomination id - or null
    /// when the whole pane should be read again.
    /// </summary>
    /// <remarks>
    /// Null is an answer rather than a gap: a write that moved several rows at
    /// once, or one whose row has no id a console could use, says so this way.
    /// </remarks>
    public string? Id { get; init; }
}

/// <summary>
/// What a change notice may be about: the panes that can go stale.
/// </summary>
/// <remarks>
/// <para>
/// <b>By pane, not by table.</b> A console decides what to re-read from this,
/// so the words are the reads it already makes - the flight list, the gates,
/// the board, the fleet - rather than how the control plane stores them.
/// </para>
/// <para>
/// <b><see cref="Ready"/> is a topic because of what it asks a console to
/// do.</b> It is the connection's first word, and whatever moved while nobody
/// was connected was never announced - so the only safe reading of "you are
/// connected now" is "read everything once".
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ChangeTopics
{
    /// <summary>The stream is open; read everything once.</summary>
    public const string Ready = "ready";

    /// <summary>A flight appeared, or its state or ending moved.</summary>
    public const string Flights = "flights";

    /// <summary>A gate opened or closed.</summary>
    public const string Gates = "gates";

    /// <summary>A nomination stood or ended.</summary>
    public const string Board = "board";

    /// <summary>A runner registered, retired, or changed what it says about itself.</summary>
    public const string Runners = "runners";

    /// <summary>Every topic a notice may carry.</summary>
    public static IReadOnlyList<string> All { get; } = [Ready, Flights, Gates, Board, Runners];
}

/// <summary>
/// The event names on the change stream, and how long it may be silent.
/// </summary>
/// <remarks>
/// <b>An event name a client does not know is skipped</b>, so a newer control
/// plane can say more than an older gg understands and the older gg keeps
/// listening. These two are the whole of what a client acts on.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ChangeEvents
{
    /// <summary>
    /// Sent once, first, with <c>{}</c> as its data. A client surfaces it as a
    /// notice whose topic is <see cref="ChangeTopics.Ready"/>.
    /// </summary>
    public const string Ready = ChangeTopics.Ready;

    /// <summary>Sent for each change, with a <see cref="ChangeNotice"/> as its data.</summary>
    public const string Changed = "changed";

    /// <summary>Every event name a client acts on.</summary>
    public static IReadOnlyList<string> All { get; } = [Ready, Changed];

    /// <summary>
    /// The longest the stream goes without writing anything; the server sends a
    /// comment line at least this often.
    /// </summary>
    /// <remarks>
    /// A stream that says nothing for minutes is indistinguishable from one a
    /// proxy cut. A client that has heard nothing for a few of these knows which
    /// it is, and reconnects.
    /// </remarks>
    public static TimeSpan Keepalive { get; } = TimeSpan.FromSeconds(15);
}
