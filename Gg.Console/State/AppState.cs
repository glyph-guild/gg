namespace Gg.Console;

public enum UiMode
{
    Normal,
    Help,
}

public enum PaneId
{
    Flights,
    Notes,
}

/// <summary>
/// The model. Plain data only — it must round-trip through JSON unchanged,
/// because the UI is torn down and rebuilt FROM this state (terminal release)
/// and views are never the source of truth.
/// </summary>
public sealed record AppState
{
    public UiMode Mode { get; init; } = UiMode.Normal;

    public PaneId FocusedPane { get; init; } = PaneId.Flights;

    public IReadOnlyList<string> Flights { get; init; } = [];

    public int SelectedFlight { get; init; }

    public string Notes { get; init; } = "";
}
