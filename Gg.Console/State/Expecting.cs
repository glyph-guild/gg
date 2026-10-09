namespace Gg.Console;

/// <summary>What the console is waiting to see, now that it has asked for it.</summary>
public enum ExpectationKind
{
    /// <summary>
    /// A flight the door accepted and no read has shown yet.
    /// </summary>
    /// <remarks>
    /// The door answers 202 before the flight is anywhere a read can see it: the
    /// command crosses to the Flight context, and the row the flight list is
    /// read from is projected when the event comes back. Measured at up to ten
    /// seconds, which is a reload that runs the moment the write returns and
    /// sees nothing.
    /// </remarks>
    FlightAppears,
}

/// <summary>
/// Something a write said it did, which the console has not seen yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question is state; the waiting is not.</b> What is expected survives a
/// session rebuild and is written into a dump, because it is on the screen
/// until it is answered. When it was first expected and when to look next are
/// times, and a time in the model is one a dump compares against a different
/// now - so those live on <c>Expectations</c>, which is owned outside every UI
/// lifetime the way <c>AutoRefresh</c>'s due time is.
/// </para>
/// </remarks>
public sealed record Expectation
{
    public required ExpectationKind Kind { get; init; }

    /// <summary>The flight id the door answered with.</summary>
    public required string Id { get; init; }
}

/// <summary>What a notification is telling somebody.</summary>
public enum NotificationKind
{
    /// <summary>A flight this console opened has appeared, and has a number.</summary>
    FlightOpened,

    /// <summary>
    /// A gate opened while somebody was watching, and it is theirs to answer.
    /// </summary>
    /// <remarks>
    /// <b>The second source, and the corner did not have to change to take
    /// it.</b> A gate arrives as the same notification a watched-for flight
    /// does, under the same keys - which is what makes this a kind rather than
    /// a second mechanism beside the first.
    /// </remarks>
    GateWaiting,

    /// <summary>
    /// A flight the door accepted has not appeared in the time it was given.
    /// </summary>
    /// <remarks>
    /// Said rather than dropped. A console that stopped looking in silence would
    /// leave a person waiting for a row that is not coming - and "accepted and
    /// not listed" is a real state worth a sentence, whatever is behind it.
    /// </remarks>
    NotListedYet,

    /// <summary>
    /// A flight somebody asked for, on its way to the door.
    /// </summary>
    /// <remarks>
    /// <b>Up the moment the key is pressed</b> (owner, 2026-10-09: when we fly
    /// something the notification should come up immediately, animated while
    /// it is submitted). It used to appear only once the flight had been
    /// accepted AND projected - up to thirty seconds of nothing after a key
    /// that looked as if it had done nothing.
    /// </remarks>
    Submitting,

    /// <summary>
    /// The door accepted it, and its number has not been minted yet.
    /// </summary>
    /// <remarks>
    /// The same notification as <see cref="Submitting"/>, one step on, and
    /// replaced in place by <see cref="FlightOpened"/> or
    /// <see cref="NotListedYet"/> - one flight is one corner entry for its whole
    /// opening, not three stacked ones.
    /// </remarks>
    Accepted,

    /// <summary>The door refused it, or never answered; nothing was opened.</summary>
    NotOpened,
}

/// <summary>What a launch asks the door for.</summary>
public enum LaunchKind
{
    /// <summary>A work item: the tracker's item is the intent.</summary>
    Ticket,

    /// <summary>Words somebody wrote, in an editor or with an agent.</summary>
    Intent,
}

/// <summary>
/// A flight somebody asked for that the door has not answered yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>State, because it is on the screen until it is answered</b> -
/// <see cref="Expectation"/>'s rule. The request itself is not: it runs on a
/// task <c>Launcher</c> owns outside every UI lifetime, so a session rebuilt
/// mid-submit neither loses the answer nor sends twice.
/// </para>
/// <para>
/// <b>Everything the door is asked with is captured here</b>, at the press. A
/// cursor that moves while the request is out must not change which item it
/// was for, and a repository chosen afterwards is for the next flight.
/// </para>
/// </remarks>
public sealed record Launch
{
    /// <summary>Which launch this is, so its answer finds its notification.</summary>
    public required Guid Token { get; init; }

    public required LaunchKind Kind { get; init; }

    /// <summary>What the corner calls it while it has no number.</summary>
    public required string Title { get; init; }

    /// <summary>The tracker, on a <see cref="LaunchKind.Ticket"/>.</summary>
    public string? Provider { get; init; }

    /// <summary>The item's id, on a <see cref="LaunchKind.Ticket"/>.</summary>
    public string? Id { get; init; }

    /// <summary>The words, on a <see cref="LaunchKind.Intent"/>.</summary>
    public string? Intent { get; init; }

    public IReadOnlyList<string> Against { get; init; } = [];

    public string? WorkKind { get; init; }

    /// <summary>
    /// Whether to ask first if this item has flown before. False once a person
    /// has answered that question.
    /// </summary>
    public bool Check { get; init; }
}

/// <summary>
/// Something the console noticed on its own, waiting for somebody to see it.
/// </summary>
/// <remarks>
/// <b>Facts, not sentences.</b> What a notification says is <c>PaneText</c>'s, from
/// these; what it names is the flight, so going to it lands on the row.
/// </remarks>
public sealed record Notification
{
    public required NotificationKind Kind { get; init; }

    /// <remarks>Empty while it is <see cref="NotificationKind.Submitting"/>: the door names it.</remarks>
    public required string FlightId { get; init; }

    /// <summary>The launch this is the corner entry for, while it is one.</summary>
    public Guid? Launch { get; init; }

    /// <summary>
    /// What the launch is called, or the door's sentence on a
    /// <see cref="NotificationKind.NotOpened"/>.
    /// </summary>
    public string? Said { get; init; }

    /// <summary>Rendered, e.g. GG-1042 - null until the flight has been seen.</summary>
    public string? FlightNumber { get; init; }

    /// <summary>What the flight is called, when it has been seen.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Which obligation is waiting, on a <see cref="NotificationKind.GateWaiting"/>.
    /// </summary>
    /// <remarks>
    /// <b>The obligation and not just the flight, because the obligation is
    /// what gets answered.</b> One flight can hold two gates open, and a corner
    /// naming only the flight would send somebody to a modal to find out which
    /// of them it meant.
    /// </remarks>
    public string? Obligation { get; init; }
}
