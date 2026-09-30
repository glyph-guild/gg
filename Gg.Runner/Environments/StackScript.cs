namespace Gg.Runner.Environments;

/// <summary>
/// The script a work kind names for bringing its stack up and down.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0023's crystalize at a second subject, in the owner's words:</b>
/// <i>instructions should crystalize into scripts, if they can, that run on the
/// runners.</i> A stack bring-up is that shape — advice an agent follows until
/// the same advice has been followed enough times to be worth writing down.
/// </para>
/// <para>
/// <b>Performed on the same terms as the agent it replaces</b> — ADR-0023 § 4:
/// same tool servers, same tier, same grant. It is narrower in two ways, both in
/// its favour: it reads no text as instructions, so there is nothing to inject
/// into, and it is deterministic.
/// </para>
/// <para>
/// <b>The path is bounded here as well as at the document.</b> An envelope is
/// validated when it is applied; a lease arrives from a control plane this binary
/// does not control. The far check is a contract and this one is a boundary, and
/// a boundary that trusts the far side is not one.
/// </para>
/// </remarks>
public static class StackScript
{
    /// <summary>Brings the stack up, before the agent works.</summary>
    public const string Up = "up";

    /// <summary>Brings it down, after the agent has finished.</summary>
    public const string Down = "down";

    /// <summary>Whether this kind names a script at all.</summary>
    /// <remarks>
    /// False is every kind in the field: the agent works the bring-up out from
    /// advice, which is where a kind stays until that advice has been used enough
    /// to be worth writing down.
    /// </remarks>
    public static bool Runs(string? stack) => !string.IsNullOrWhiteSpace(stack);

    /// <summary>The argument this verb is performed with.</summary>
    public static string ArgumentFor(string verb) => verb;

    /// <summary>
    /// Where the script would be inside this tree, or null when the path leaves
    /// it.
    /// </summary>
    /// <remarks>
    /// <b>Resolved and compared, rather than inspected for <c>..</c>.</b> A
    /// segment check is a check of spelling; what matters is where the path
    /// lands, and only the filesystem's own resolution answers that. It says
    /// where the script WOULD be — whether it is there is the caller's to find
    /// out, because a document valid when it was applied describes a repository
    /// that has since moved.
    /// </remarks>
    public static string? Within(string tree, string? stack)
    {
        if (!Runs(stack) || string.IsNullOrWhiteSpace(tree))
        {
            return null;
        }

        var root = Path.GetFullPath(tree);
        var full = Path.GetFullPath(Path.Combine(root, stack!));

        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : null;
    }


    /// <summary>How long a bring-up may take before it is abandoned.</summary>
    /// <remarks>
    /// A stack of thirteen containers pulling cold images is minutes, not
    /// seconds; a script that has hung is not worth a flight's whole budget. Ten
    /// minutes is the first number, and the walk is what moves it.
    /// </remarks>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(10);

    /// <summary>Performs one verb of this script, inside the tree.</summary>
    public static async Task PerformAsync(
        string script, string verb, string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = script.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                ? "pwsh"
                : script,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        if (start.FileName == "pwsh")
        {
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
        }

        start.ArgumentList.Add(verb);

        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new IOException($"'{script}' did not start.");

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        await process.WaitForExitAsync(patience.Token);
    }
}
