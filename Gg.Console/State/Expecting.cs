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

    /// <summary>
    /// A gate this console answered, and the gate list still showing it.
    /// </summary>
    /// <remarks>
    /// The door answers 202 and the gate closes a moment later, when the
    /// decision's cascade reaches the receptor that closes it - in-process, but
    /// behind a flush window. The console used to hold the screen for up to
    /// thirty seconds watching for that; it answers now and looks.
    /// </remarks>
    GateAnswered,
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

    /// <summary>
    /// The flight id the door answered with - or, for a gate, the flight number
    /// the gate list names it by.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>For a gate, which of the flight's obligations was answered.</summary>
    public string? Obligation { get; init; }
}

/// <summary>What a notification is telling somebody.</summary>
public enum NotificationKind
{
    /// <summary>A flight this console opened has appeared, and has a number.</summary>
    FlightOpened,

    /// <summary>
    /// A flight the door accepted has not appeared in the time it was given.
    /// </summary>
    /// <remarks>
    /// Said rather than dropped. A console that stopped looking in silence would
    /// leave a person waiting for a row that is not coming - and "accepted and
    /// not listed" is a real state worth a sentence, whatever is behind it.
    /// </remarks>
    NotListedYet,

    /// <summary>A gate this console answered has closed.</summary>
    GateAnswered,

    /// <summary>
    /// A gate this console answered is still showing after the time it was given.
    /// </summary>
    /// <remarks>
    /// Not a refusal - nothing has said no - and said for <see cref="NotListedYet"/>'s
    /// reason: going quiet about something a person is waiting on is worse than
    /// saying it has not happened yet.
    /// </remarks>
    GateStillWaiting,

    /// <summary>
    /// A gate this console did not answer has opened, and is waiting on somebody.
    /// </summary>
    /// <remarks>
    /// The one kind that is not about this console's own writes: the queue grew
    /// while a person was doing something else, which is exactly what a corner
    /// that can be glanced at is for.
    /// </remarks>
    GateOpened,
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

    public required string FlightId { get; init; }

    /// <summary>Rendered, e.g. GG-1042 - null until the flight has been seen.</summary>
    public string? FlightNumber { get; init; }

    /// <summary>What the flight is called, when it has been seen.</summary>
    public string? Name { get; init; }
}
