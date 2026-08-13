namespace Gg.Console;

/// <summary>
/// Everything the keymap can produce. Bindings live in the keymap; meanings
/// live here; effects live in the reducer.
/// </summary>
public enum Command
{
    Quit,
    ToggleHelp,

    /// <summary>What can be done to the selected flight.</summary>
    ToggleFlightActions,

    /// <summary>The one escape hatch out of whichever modal is open.</summary>
    CloseModal,

    FocusNextPane,
    SelectNext,
    SelectPrevious,

    ToggleEvidence,

    /// <summary>Attach or detach the live view. Recorded as a fact.</summary>
    ToggleLive,

    /// <summary>Hold the live view still so text can be selected.</summary>
    ToggleFreeze,

    OpenEditor,

    /// <summary>
    /// Take the selected flight over: unmount, hand a person the terminal, come
    /// back to the same state.
    /// </summary>
    /// <remarks>
    /// Only for a flight whose loop has ended. Interrupting a running one is a
    /// handoff rather than a steering wheel, and it is a different feature.
    /// </remarks>
    TakeFlight,

    /// <summary>
    /// Hand a taken flight back: the agent proposes an account of what you did,
    /// and you confirm it.
    /// </summary>
    /// <remarks>
    /// Only for a flight somebody has taken over. Nothing resumes the loop - the
    /// account is recorded and the flight ends - so what this buys is a record
    /// the next reader finds, which is the next takeover.
    /// </remarks>
    HandBack,
}
