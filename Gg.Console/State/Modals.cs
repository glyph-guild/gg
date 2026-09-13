namespace Gg.Console;

/// <summary>
/// Which modes are drawn as a dialog over the tab.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three ratchets assumed "not Normal" meant "a dialog", and it stopped
/// being true.</b> Every mode but one owns the keyboard by drawing a frame with
/// a title, a body and its keys as buttons — so
/// <c>_modal.Visible = Mode != Normal</c> was right, and the tests that walk
/// every mode demanding a title, an arm and the focus were right with it.
/// <c>AirspacePath</c> owns the keyboard through a field inside a pane instead,
/// and under the old rule it drew an empty dialog on top of the field it was
/// trying to focus.
/// </para>
/// <para>
/// <b>Stated, never derived.</b> The alternative was deriving this from whether
/// a mode has a title, which reads well and hides the decision: a mode with no
/// title would silently become a non-dialog rather than failing the ratchet
/// that exists to notice it. The same argument <c>OfferableKeys</c> makes about
/// its own list.
/// </para>
/// <para>
/// <b>Every mode is in exactly one of these two, and a test holds that</b> — so
/// the next mode that is not a dialog has to say why here, rather than
/// discovering the three failures this one did.
/// </para>
/// </remarks>
public static class Modals
{
    /// <summary>Every mode drawn as a dialog, with a title and its keys.</summary>
    public static IReadOnlyList<UiMode> Drawn { get; } =
    [
        UiMode.Help,
        UiMode.FlightActions,
        UiMode.AirspaceActions,
        UiMode.FlightDetail,
        UiMode.Runner,
        UiMode.HandFlight,
        UiMode.ConfirmFlight,
        UiMode.ConfirmGround,
        UiMode.ConfirmFlyAgain,
        UiMode.ConfirmApply,
        UiMode.GateDecision,
        UiMode.SignIn,
        UiMode.ReadingEnvelope,
        UiMode.ReadingChangeset,
        UiMode.ReadingOutcome,
        UiMode.ConfirmRetire,
        UiMode.ComposeChoice,

        // ARRIVED ON MAIN WHILE THIS BRANCH WAS OUT, and the ratchet in this
        // file is what asked. It is ComposeChoice's shape applied to a number -
        // a few shares and a key each - so it is a question with a title, a
        // body and labelled answers, which is a dialog.
        UiMode.FloorChoice,

        // A QUESTION WITH A LIST IN IT, which is ComposeChoice's shape over
        // names the tenant declared rather than over two fixed answers. A title,
        // a body and a cursor: a dialog.
        UiMode.WorkKindChoice,

        // A DOCUMENT WITH A TITLE, which is what the reading views are. What it
        // holds is one work item as its own reader rendered it.
        UiMode.WorkItemDetail,
    ];

    /// <summary>
    /// The modes that own the keyboard without drawing a dialog, and why.
    /// </summary>
    /// <remarks>
    /// Kept as data with the reason attached, so an addition has to be argued
    /// rather than quietly appended — <c>VerbParityTests</c>' shape, one screen
    /// over.
    /// </remarks>
    public static IReadOnlyDictionary<UiMode, string> NotDrawn { get; } =
        new Dictionary<UiMode, string>
        {
            [UiMode.Normal] = "nothing owns the keyboard; the keymap answers the tab.",

            [UiMode.AirspacePath] =
                "a field at the bottom of the airspace tab owns it. Drawing a dialog would "
              + "cover the field the mode exists to focus, and a title over one input is "
              + "ceremony around a path.",
        };

    /// <summary>Whether this mode draws a dialog.</summary>
    public static bool IsDrawn(UiMode mode) => Drawn.Contains(mode);
}
