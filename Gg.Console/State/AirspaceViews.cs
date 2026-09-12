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
    /// <b>ON DISK ONLY WHEN IT DIFFERS.</b> A file matching what is applied
    /// makes the first two the same document, and a tab that duplicates its
    /// neighbour teaches people to stop reading tabs. It appears when there is
    /// something to compare — which is also the only time somebody is asking.
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

        var applied = Applied(state, pointed.Name);

        // WHOSE ANSWER "DIFFERS" IS. Not this console's: comparing the raw
        // file against the canonical rendering would call every pulled
        // document different, because the renderer normalises what an author
        // wrote. The diff verb already answers it, git answers the local half,
        // and a name with nothing applied differs by construction.
        //
        // THE SAME RULE THE ROWS FOLLOW. Direction is read and never computed
        // here, for ADR-0016 § 6's reason, and this is the same question one
        // step smaller.
        var differs = applied is null
            || state.Estate?.Working?.Changes.Any(c =>
                string.Equals(c.Path, pointed.Path, StringComparison.Ordinal)) is true
            || state.Estate?.Uncommitted.Contains(pointed.Path, StringComparer.Ordinal)
                is true;

        return
        [
            .. differs ? (AirspaceView[])[AirspaceView.OnDisk] : [],
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
