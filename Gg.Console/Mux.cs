namespace Gg.Console;

/// <summary>
/// The agents gg holds beside itself (slice sixty-nine). Declared so the criteria compile; nothing
/// is held yet.
/// </summary>
public sealed class Mux
{
    public Mux(
        Func<IHostTerminal?>? terminal = null,
        MuxLedger? ledger = null,
        Func<DateTimeOffset>? clock = null,
        string? agentCommand = null)
    {
    }

    public bool Any => false;

    public bool Ending => false;

    public MuxLedger? Ledger => null;

    public IReadOnlyList<MuxRow> Rows() => [];

    public IReadOnlyList<string> Labels() => [];

    public string Screen(int number) => "";

    public void Want(MuxTab tab)
    {
    }

    public MuxTab? TakeWanted() => null;

    public AppState Fold(AppState state, out bool folded)
    {
        folded = false;
        return state;
    }

    public void Launch(string label, Func<Func<AppState, AppState>> work)
    {
    }

    public HostRun Host => PtyHost.RunAsync;

    public object? StartClaudeCode(string workingDirectory, string? resume = null) => null;

    public void EndAll()
    {
    }

    public Mux Reading(Gg.Client.ItineraryDrafts drafts, Func<string, Gg.Contracts.BoardPage?> plan) => this;

    public MuxLeave Show(MuxTab tab) => MuxLeave.Gg;
}

/// <summary>What showing the mux ends in.</summary>
public enum MuxLeave
{
    Gg,
    Plan,
    Compose,
}
