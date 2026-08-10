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
}
