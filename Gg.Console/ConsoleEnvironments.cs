using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Reading the chart, what furnishes it, and what the pools last said.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ConsoleRepositories</c>' shape, for the same reason.</b> Showing this
/// pane is a read and a UI session may not make one, so the read happens beside
/// the session and the answer goes through <c>ConsoleProjection.Apply</c>
/// rather than a bespoke reducer — one path from a verb result into the model.
/// </para>
/// <para>
/// <b>THREE READS, AND THE FIRST ONE DECIDES WHETHER THE OTHERS MATTER.</b>
/// The chart is the list of rows. A strategy for a name that is not charted
/// draws nothing, and an attestation for a pool no strategy names draws nothing
/// either — so the chart is fetched first, and the two joins are allowed to
/// fail on their own.
/// </para>
/// <para>
/// <b>AND THE LEDGER IS THE ONE MOST LIKELY TO FAIL.</b> <c>GET /v1/pools</c>
/// was declared for slices with nothing calling it, so this is the first gg
/// that asks — and a control plane that has not implemented it answers
/// something outside the endpoint's declared statuses. Losing the attestation
/// column is the right cost; losing the chart over it is not.
/// </para>
/// <para>
/// <b>A refusal leaves the chart null and says why.</b> Recording an empty
/// chart instead would render as "this tenant has charted nothing", which sends
/// a person to chart a name they already have — <c>ConsoleRepositories</c>'
/// rule, one read over.
/// </para>
/// </remarks>
public static class ConsoleEnvironments
{
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        AppState charted;

        try
        {
            charted = ConsoleProjection.Apply(
                state, data.EnvironmentsAsync().GetAwaiter().GetResult());
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            return state with
            {
                Diagnosis = "Could not read the environment chart: " + failure.Message,
            };
        }

        return Joined(data, Joined(data, charted, data.StrategiesAsync), data.PoolsAsync);
    }

    /// <summary>
    /// Folds one of the two joins, or leaves the state as it was.
    /// </summary>
    /// <remarks>
    /// <b>Silently, and that is the whole intent.</b> What a failed join costs
    /// is a column, and the row it belongs to says so already by being empty —
    /// which is the same sentence an unfurnished name gets, and the right one:
    /// nobody has told this console what furnishes it. A diagnosis here would
    /// overwrite the chart's own, which is the one worth reading.
    /// </remarks>
    private static AppState Joined(
        ConsoleData data, AppState state, Func<CancellationToken, Task<VerbResult>> read)
    {
        try
        {
            return ConsoleProjection.Apply(
                state, read(CancellationToken.None).GetAwaiter().GetResult());
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            return state;
        }
    }
}
