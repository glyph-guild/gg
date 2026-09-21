using Gg.Contracts;

namespace Gg.Runner.Intent;

/// <summary>
/// A declared tracker this machine cannot open, which refuses the write rather
/// than the start-up.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 2 of slice forty-seven, and the shape it takes.</b> A tracker's
/// credential is resolved on the machine, so a profile can declare one this
/// machine has not been given. <see cref="WiqlWorkItemSink"/> refuses an absent
/// credential at construction - correctly, because a sink that cannot
/// authenticate is not a sink - and every declared sink is built at once, so one
/// unresolvable secret used to throw where the runner is composed.
/// </para>
/// <para>
/// <b>An offer must not be able to stop a runner from starting.</b> Its own
/// justification was that the throw lands "in front of the person configuring
/// the machine"; on a fleet host that person is a systemd unit, and a machine
/// that will not start cannot be told anything, including that it was wrong.
/// </para>
/// <para>
/// <b>Why an entry at all, rather than leaving the destination out.</b> The
/// loop's refusal for an unknown destination says the runner "has no tracker
/// declared for it" and points at <c>tracker-apis</c>. That sentence would be
/// false here - the tracker IS declared - and would send somebody to edit a
/// document that is already right. What is missing is a secret on this machine,
/// so this says so, and names the reference to put there.
/// </para>
/// </remarks>
/// <param name="destination">The destination id an envelope names.</param>
/// <param name="reference">The credential reference that did not resolve.</param>
public sealed class LackingWorkItemSink(string destination, string reference) : IWorkItemSink
{
    private readonly string _destination = destination;
    private readonly string _reference = reference;

    public Task<IReadOnlyList<WorkItemWrite>> PerformAsync(
        IReadOnlyList<WorkItemProposal> admitted,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            $"This runner is declared to write to '{_destination}' and cannot: the credential "
          + $"'{_reference}' does not resolve on this machine. The tracker itself is configured "
          + "correctly - what is missing is the secret, which is the developer's, registered with "
          + "`gg credential add` and carrying write scope, or sent with `gg credential send "
          + "--runner`. Nothing was written.");
}
