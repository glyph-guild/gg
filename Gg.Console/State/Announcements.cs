namespace Gg.Console;

/// <summary>
/// Gates that have started needing this person since the console last looked.
/// </summary>
/// <remarks>
/// <para>
/// <b>An announcement is a transition, never a standing state.</b> That is the
/// whole rule, and it is what separates this from <c>AppState.Notices</c> —
/// the tenant's standing notices, which say what IS true. This says what has
/// just BECOME true, while somebody was watching.
/// </para>
/// <para>
/// <b>Which is why the first look announces nothing.</b> Eleven gates were
/// waiting on this tenant the day this was written, the oldest six days old.
/// Opening the console is not the moment any of them happened, and a person
/// greeted by their whole backlog learns to dismiss the corner unread.
/// </para>
/// <para>
/// <b>Into the corner <see cref="Expectations"/> already owns.</b> A gate
/// becomes the same <see cref="Notification"/> a watched-for flight does, with
/// its own <see cref="NotificationKind"/>, so it pages, dismisses and is gone
/// after the same quiet under the same keys. A second delivery would have been
/// a second thing to learn and a second thing to maintain.
/// </para>
/// <para>
/// <b>Pure, and over one state.</b> No clock, no widget, no read. Where "live"
/// actually gets decided is the two callers: <c>ConsoleStart</c> sets
/// <c>Gates</c> once at boot and <c>ConsoleRefresh</c> sets it on every tick
/// afterwards, so the first fold arms this and the rest are news.
/// </para>
/// </remarks>
public static class Announcements
{
    /// <summary>
    /// What to say now, and what to remember for next time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null in, null out, and that is load-bearing.</b> <c>Gates</c> is null
    /// until the console has asked, and an absent answer must not read as
    /// "there are none" — that would make the first real read announce
    /// everything.
    /// </para>
    /// <para>
    /// <b>Only gates this person may answer.</b> Being told about a decision
    /// you cannot make is an interruption with no act at the end of it. And a
    /// gate that is somebody else's is not remembered either: remembering it
    /// would leave it silent forever if it were later spelled to me.
    /// </para>
    /// </remarks>
    public static (IReadOnlyList<Notification> Say, IReadOnlyList<string>? Remember) Arrived(
        AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Gates is not { } list)
        {
            // NOT ASKED YET. The previous memory is kept rather than cleared: a
            // read that failed is not an answer that there are none.
            return ([], state.Announced);
        }

        var mine = list.Gates
            .Where(gate => string.Equals(gate.Approver, state.Principal, StringComparison.Ordinal))
            .ToList();

        var remember = mine.Select(KeyOf).ToArray();

        if (state.Announced is not { } already)
        {
            // THE BASELINE. Remember everything, say nothing.
            return ([], remember);
        }

        var said = mine
            .Where(gate => !already.Contains(KeyOf(gate), StringComparer.Ordinal))
            .Select(Of)
            .ToArray();

        return (said, remember);
    }

    /// <summary>
    /// The same thing, folded onto the state the console carries.
    /// </summary>
    /// <remarks>
    /// <b>The same state back when there is no news</b>, so a refresh tick that
    /// found nothing costs no rebuild — and the reference check is what the
    /// test asserts, rather than a flag somebody has to remember to set.
    /// <para>
    /// <b>The cursor moves to the newest unless somebody is reading.</b>
    /// <see cref="Expectations"/> makes exactly this choice for its own
    /// notifications, and a corner that paged itself out from under a person
    /// would be worse for arriving from a second source.
    /// </para>
    /// </remarks>
    public static AppState Folded(AppState state)
    {
        var (say, remember) = Arrived(state);

        // NO NEWS, NO NEW STATE. Compared by CONTENTS and not by reference:
        // Arrived builds a fresh array every call, so a reference check is
        // false on every quiet tick and this would hand back a rebuilt state
        // for ever. The test that asks for the same reference back is what
        // caught it.
        var same = say.Count == 0
            && (ReferenceEquals(remember, state.Announced)
                || (remember is not null
                    && state.Announced is not null
                    && remember.SequenceEqual(state.Announced, StringComparer.Ordinal)));

        if (same)
        {
            return state;
        }

        return state with
        {
            Notifications = say.Count == 0 ? state.Notifications : [.. state.Notifications, .. say],
            NotificationAt = say.Count == 0 || state.Mode == UiMode.Notifications
                ? state.NotificationAt
                : state.Notifications.Count,
            Announced = remember,
        };
    }

    private static string KeyOf(Gg.Contracts.PendingGate gate) =>
        $"{gate.FlightNumber}/{gate.ObligationId}";

    private static Notification Of(Gg.Contracts.PendingGate gate) => new()
    {
        Kind = NotificationKind.GateWaiting,

        // THE NUMBER IN BOTH, because a gate is all this console knows about
        // the flight: the list carries GG-247 and no id, and going to it looks
        // the number up the way every other row does.
        FlightId = gate.FlightNumber,
        FlightNumber = gate.FlightNumber,
        Obligation = gate.ObligationId,
    };
}
