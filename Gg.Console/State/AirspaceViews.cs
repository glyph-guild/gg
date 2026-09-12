namespace Gg.Console;

/// <summary>
/// Which of the three questions the airspace tab's right-hand pane is
/// answering about the selected row.
/// </summary>
/// <remarks>
/// <b>Three different answers, not three renderings of one.</b> What the file
/// says is what somebody edits; what the airspace holds is what landed; what
/// composes is what actually governs a flight. Being unable to tell those
/// apart is what made an applied document look like it had never landed.
/// </remarks>
public enum AirspaceView
{
    /// <summary>The file, verbatim, comments and all.</summary>
    OnDisk,

    /// <summary>The document the airspace holds, with the version naming it.</summary>
    Applied,

    /// <summary>The floor composed with it — what governs a flight of this kind.</summary>
    Effective,
}

/// <summary>
/// Which views the selected row actually has, and how to move between them.
/// </summary>
/// <remarks>
/// <b>Pure, and asked by the keymap as well as the pane.</b> Which key is
/// offered and which view is drawn are the same question, and two answers to
/// it would drift the first time a row gained or lost one.
/// </remarks>
public static class AirspaceViews
{
    /// <summary>
    /// The views this row has, in the order the work flows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ON DISK ALWAYS.</b> This used to appear only when the file differed
    /// from what is applied, on the argument that a tab duplicating its
    /// neighbour teaches people to stop reading tabs. In use it did something
    /// else: it took the FILE away from a tree of file NAMES. Select a
    /// document that is committed and applied - the ordinary, healthy state -
    /// and there was no way to look at it.
    /// <para>
    /// The duplication was never real either. One of these is what somebody
    /// wrote, comments and ordering and all; the other is what gg made of it.
    /// </para>
    /// </para>
    /// <para>
    /// <b>EFFECTIVE ONLY FOR A WORK KIND.</b> Composition is per work kind
    /// because that is what a flight has; there is no such question about a
    /// strategy, and an empty tab would be answering one nobody asked.
    /// </para>
    /// <para>
    /// <b>Applied is always offered</b>, even when nothing is applied to the
    /// name — because "nothing is applied to this yet" is the answer somebody
    /// needs, and a missing tab would leave them guessing which of the two
    /// kinds of nothing they were in.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<AirspaceView> Offered(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (AirspaceRows.Pointed(state) is not { } pointed)
        {
            return [];
        }

        return
        [
            AirspaceView.OnDisk,
            AirspaceView.Applied,
            .. string.Equals(pointed.Role, Gg.Contracts.Roles.WorkKind, StringComparison.Ordinal)
                ? (AirspaceView[])[AirspaceView.Effective]
                : [],
        ];
    }

    /// <summary>What the tab for a view says.</summary>
    /// <remarks>
    /// <b>Lower case, and three words a person would use.</b> These sit along
    /// the foot of a pane beside a tree of file names; a heading voice there
    /// would compete with the thing being read. And they are here rather than
    /// in the screen for the reason every other title in this console is pure:
    /// the screen cannot be constructed without a terminal, so nothing can ask
    /// it what it drew.
    /// </remarks>
    public static string Title(AirspaceView view) => view switch
    {
        AirspaceView.OnDisk => "on disk",
        AirspaceView.Effective => "effective",
        _ => "applied",
    };

    /// <summary>The next view round, or the first when this one is gone.</summary>
    /// <remarks>
    /// <b>Clamped rather than wrapped from a stale value.</b> A row whose
    /// on-disk view disappeared — because somebody applied it — must not leave
    /// the pane pointing at a tab that is no longer offered.
    /// </remarks>
    public static AirspaceView Next(AppState state, AirspaceView showing)
    {
        ArgumentNullException.ThrowIfNull(state);

        var offered = Offered(state);

        if (offered.Count == 0)
        {
            return AirspaceView.Applied;
        }

        var at = offered.ToList().IndexOf(showing);

        return at < 0 ? offered[0] : offered[(at + 1) % offered.Count];
    }

    /// <summary>The applied document for a name, or null.</summary>
    public static Gg.Contracts.NamedEnvelopeState? Applied(AppState state, string name)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Estate?.Applied.FirstOrDefault(
            d => string.Equals(d.Name, name, StringComparison.Ordinal));
    }

    /// <summary>The applied document as text, or null when there is none.</summary>
    internal static string? Rendered(Gg.Contracts.NamedEnvelopeState? applied) =>
        applied?.Narrowing is { } narrowing
            ? Gg.Contracts.EnvelopeText.Render(narrowing)
            : applied?.Envelope is { } envelope
                ? Gg.Contracts.EnvelopeText.Render(envelope)
                : null;

}
