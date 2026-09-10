using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Intent;

/// <summary>
/// One change that was admitted, and what performing it produced.
/// </summary>
/// <param name="Operation">
/// Which of the operations was performed, as the contract spells it.
/// </param>
/// <param name="Target">
/// The item it happened to. <b>Always present here, even for a create</b>,
/// because the whole point of reporting a create is the id the tracker issued -
/// a create that reported nothing would leave an item nothing can find again.
/// </param>
/// <param name="Url">Where a person can look at it, or null where the tracker gave none.</param>
/// <param name="AlreadyDone">
/// Whether this was found rather than made.
/// </param>
/// <remarks>
/// <b><see cref="AlreadyDone"/> is a member rather than a silence.</b> A retry
/// that finds the earlier change and one that makes a new one both end with the
/// tracker in the right state, and only one of them is worth a person knowing
/// about - a run reporting every write as new, on a retry, is a run whose report
/// of what it did is a guess.
/// </remarks>
public sealed record WorkItemWrite(
    string Operation,
    string Target,
    string? Url,
    bool AlreadyDone);

/// <summary>
/// Where admitted changes to work items are performed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The write half of <see cref="IWorkItemSource"/>, and NOT beside it.</b>
/// The read seam lives in <c>Gg.Local</c>, which holds no project reference by
/// charter - a filesystem convention two halves of gg share, and nothing that
/// reads the wire contract. This one names <see cref="WorkItemProposal"/>, so it
/// is here for the reason <c>NominationTool.Unservable</c> is: the charter says
/// where a type that reads <c>Gg.Contracts</c> goes, and it is not there.
/// A separate interface on purpose, though. A runner configured to read a tracker and not to
/// write to one holds a source and no sink, so "no destination, no write" is
/// true at the level of which objects exist rather than at the level of a check
/// somebody could delete. That is the disposition <c>DestinationConfiguration</c>
/// already gives the git side, one system over.
/// </para>
/// <para>
/// <b>It takes what was ADMITTED, never what was proposed.</b> The filtering
/// happened at the control plane, against a menu a person wrote; an
/// implementation that took proposals and an admission and worked out the
/// intersection would be a second copy of that rule, living in the process the
/// threat model trusts least.
/// </para>
/// </remarks>
public interface IWorkItemSink
{
    /// <summary>
    /// Performs every admitted change, in order, and reports what each did.
    /// </summary>
    /// <param name="admitted">
    /// The changes admission said may be performed. Every one of them is
    /// performed and nothing else is.
    /// </param>
    /// <param name="idempotencyKey">
    /// What ties a created item back to the flight that asked for it, so a
    /// retry finds it rather than making a second.
    /// </param>
    /// <remarks>
    /// <b>Idempotent across the seam</b>, on <c>IDestinationAdapter</c>'s
    /// existing rule: the write can succeed and the report of it fail, the
    /// batch is retried, and a retry must find the earlier change rather than
    /// make a second. Setting a field twice is one field and needs nothing;
    /// creating an item twice is two items, which is what the key is for.
    /// </remarks>
    Task<IReadOnlyList<WorkItemWrite>> PerformAsync(
        IReadOnlyList<WorkItemProposal> admitted,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Which trackers this runner may WRITE to, and where their apis are.
/// </summary>
/// <remarks>
/// <para>
/// <b>A second declaration, and that is the point</b> —
/// <c>DestinationConfiguration</c>'s argument one system over. Reading a
/// tracker and writing to one are different permissions on different
/// credentials, so a runner configured to read holds no sink: <i>no
/// declaration, no write</i> is true at the level of which objects exist,
/// rather than at the level of a check somebody could delete.
/// </para>
/// <para>
/// <b>Keyed by the destination id an envelope names</b>, because that is what
/// an admission comes back carrying. Keying by host would make one tracker two
/// destinations indistinguishable, which is the thing a menu on each of them
/// exists to keep apart.
/// </para>
/// <para>
/// <b>Absent entirely is ordinary rather than degraded.</b> The same binary
/// runs on a machine that triages and one that never will, and gg names no
/// tracker.
/// </para>
/// </remarks>
public static class TrackerConfiguration
{
    /// <summary>The variable naming which trackers this runner may write to.</summary>
    public const string ApisVariable = "GG_TRACKER_APIS";

    /// <summary>The sinks this environment describes, by destination id.</summary>
    /// <param name="clientFor">The client to speak to a host through.</param>
    /// <param name="apis">
    /// The declaration, or null to read <see cref="ApisVariable"/>. Passed by
    /// tests; the root reads the environment through the one reader.
    /// </param>
    /// <param name="secretFor">
    /// The credential registered for a destination, resolved on this machine.
    /// </param>
    /// <remarks>
    /// <b>A declared api with no credential THROWS.</b> The sink refuses an
    /// absent credential at construction, and this is where that refusal
    /// belongs: at start-up, in front of the person configuring the machine,
    /// rather than at the first admitted write in front of nobody.
    /// </remarks>
    public static IReadOnlyDictionary<string, IWorkItemSink> FromEnvironment(
        Func<string, HttpClient> clientFor,
        string? apis = null,
        Func<string, string?>? secretFor = null)
    {
        ArgumentNullException.ThrowIfNull(clientFor);

        var declared = apis ?? Environment.GetEnvironmentVariable(ApisVariable) ?? "";
        var sinks = new Dictionary<string, IWorkItemSink>(StringComparer.Ordinal);

        foreach (var entry in declared.Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = entry.IndexOf('=', StringComparison.Ordinal);

            if (split <= 0 || split == entry.Length - 1)
            {
                throw new InvalidOperationException(
                    $"{ApisVariable} entry '{entry}' is not `destination=uri`. Each entry names "
                  + "the destination id an envelope declares and the tracker root to write to.");
            }

            var id = entry[..split];
            var host = entry[(split + 1)..];

            sinks[id] = new WiqlWorkItemSink(host, secretFor?.Invoke(id), clientFor(host));
        }

        return sinks;
    }
}
