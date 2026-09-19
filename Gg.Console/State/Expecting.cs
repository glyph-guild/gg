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
