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
