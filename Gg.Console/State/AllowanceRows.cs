namespace Gg.Console;

/// <summary>
/// Which allowance the machine a person is looking at spends from.
/// </summary>
/// <remarks>
/// <b>One derivation, three readers.</b> The runners pane renders a share
/// beside a machine, the keymap decides whether the floor key is live, and the
/// modal names the subscription it is asking about — and all three have to
/// agree about which allowance the selected row belongs to. Two of them
/// disagreeing is how a person comes to set a floor on something other than
/// what the screen said.
/// </remarks>
public static class AllowanceRows
{
    /// <summary>
    /// The allowance the selected runner reports, or null.
    /// </summary>
    /// <remarks>
    /// Null covers three states deliberately: no runner selected, a runner
    /// that reports no allowance, and a control plane too old to serve any.
    /// None of them is a floor anybody can set, which is the only question
    /// this answers.
    /// </remarks>
    public static string? SelectedName(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Rows.Selected(state) is not { } row) { return null; }

        return For(state, row.Id)?.Name;
    }

    /// <summary>What one machine's allowance has reported, or null.</summary>
    public static Gg.Contracts.AllowanceSummary? For(AppState state, string runnerId)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Allowances?.Allowances.FirstOrDefault(
            a => a.Runners.Contains(runnerId, StringComparer.Ordinal));
    }
}
