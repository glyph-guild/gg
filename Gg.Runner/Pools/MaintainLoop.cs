using Gg.Contracts;

namespace Gg.Runner.Pools;

/// <summary>
/// The resident runner's routine tier: probe the scope once, then verify,
/// pull, act and attest until cancelled. Mints no flights — the next
/// flight's facts are the audit trail, and what cannot attest escalates
/// control-plane-side.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bound is the precondition.</b> A session opens by reaching outside
/// the pool prefix and expecting the proxy's refusal; a session that was not
/// refused attests the failure (so escalation has something to read) and
/// exits 69 without acting — § 12 on infrastructure, slice eleven's shape.
/// The probe's instant stamps every attestation the session ships, because
/// the decider's outward-act rule reads it.
/// </para>
/// <para>
/// Time enters through <see cref="IClock"/> and waiting through a delegate,
/// the runner loop's own rule.
/// </para>
/// </remarks>
public sealed class MaintainLoop(
    IPoolProtocol protocol,
    IPoolAdapter adapter,
    IClock clock,
    Func<TimeSpan, CancellationToken, Task> delay)
{
    private readonly IPoolProtocol _protocol = protocol;
    private readonly IPoolAdapter _adapter = adapter;
    private readonly IClock _clock = clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay;

    /// <summary>How long between cycles. Injected waiting makes it a test's choice too.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(5);

    /// <summary>Runs until cancelled. 0 is a session that ended; 69 is a bound that broke.</summary>
    public async Task<int> RunAsync(string pool, CancellationToken cancellationToken)
    {
        // BEFORE ANYTHING IS ASKED OF THE PROXY. A pool that cannot pass its
        // create rule would be refused with a 403 - which is exactly what a
        // correct out-of-scope refusal looks like, and what ProbeScopeAsync
        // treats as proof the bound holds. Refusing here keeps those two apart.
        pool = PoolNaming.Require(pool);

        var probe = await _adapter.ProbeScopeAsync(cancellationToken);
        if (!probe.Held)
        {
            // The failure CROSSES before the stop: escalation reads the
            // ledger, and a silent exit would be nothing-arrived-nothing-
            // complained. No scope stamp - a broken probe proves nothing,
            // and stamping it would let a refusal read as a proof.
            await _protocol.AttestAsync(pool, new PoolAttestation
            {
                AttestationId = Guid.CreateVersion7(),
                Pool = pool,
                Action = PoolActions.Verify,
                Outcome = PoolOutcomes.Failed,
                MeasuredAt = _clock.UtcNow,
                Diagnosis = probe.Diagnosis
                    ?? "the scope probe did not hold and did not say why.",
            }, cancellationToken);

            return 69;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var members = await _adapter.ListAsync(pool, cancellationToken);

            // THE EMPTY POOL ANNOUNCES ITSELF. The first attestation is the
            // pull point coming up - it recovers bring-up, and it carries the
            // scope stamp the decider requires before any outward act. A loop
            // that stayed silent until a member existed deadlocked the whole
            // management story at birth (found live, by the walk).
            if (members.Count == 0)
            {
                await AttestAsync(pool, PoolActions.Verify, new PoolObservation
                {
                    Outcome = PoolOutcomes.Verified,
                }, probe, actionId: null, cancellationToken);
            }

            foreach (var member in members)
            {
                var observed = await _adapter.VerifyAsync(member, cancellationToken);
                await AttestAsync(pool, PoolActions.Verify, observed, probe, actionId: null,
                    cancellationToken);
            }

            var decided = await _protocol.PullActionsAsync(pool, cancellationToken);
            foreach (var action in decided.Actions)
            {
                var observed = await ExecuteAsync(pool, action, cancellationToken);
                await AttestAsync(pool, action.Action, observed, probe, action.ActionId,
                    cancellationToken);
            }

            try
            {
                await _delay(PollEvery, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return 0;
    }

    private async Task<PoolObservation> ExecuteAsync(
        string pool, PoolAction action, CancellationToken cancellationToken)
    {
        if (string.Equals(action.Action, PoolActions.Verify, StringComparison.Ordinal))
        {
            // A decided verify is unusual but honest: inspect the first
            // member, or say there is nothing to inspect.
            var members = await _adapter.ListAsync(pool, cancellationToken);
            return members is [var first, ..]
                ? await _adapter.VerifyAsync(first, cancellationToken)
                : new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = $"a verify was decided for '{pool}' and the pool has no members.",
                };
        }

        if (action.Image is not { Length: > 0 } image)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"a {action.Action} was decided without an image. It converges on "
                          + "nothing, so nothing was done.",
            };
        }

        if (string.Equals(action.Action, PoolActions.Refresh, StringComparison.Ordinal))
        {
            var member = await NextSlotAsync(pool, cancellationToken);
            return await _adapter.RefreshAsync(pool, member, image, cancellationToken);
        }

        if (string.Equals(action.Action, PoolActions.Reset, StringComparison.Ordinal))
        {
            var members = await _adapter.ListAsync(pool, cancellationToken);
            return members is [var first, ..]
                ? await _adapter.ResetAsync(first.Name, image, cancellationToken)
                : new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = $"a reset was decided for '{pool}' and the pool has no members.",
                };
        }

        return new PoolObservation
        {
            Outcome = PoolOutcomes.Failed,
            Diagnosis = $"'{action.Action}' is not an action this runner knows how to take.",
        };
    }

    /// <summary>
    /// The lowest member index not present: containers are cattle, named
    /// <c>&lt;pool&gt;-1..N</c>, and the bound on N is the decider's — a
    /// refresh is only ever decided inside the strategy's inventory.
    /// </summary>
    private async Task<string> NextSlotAsync(string pool, CancellationToken cancellationToken)
    {
        var taken = (await _adapter.ListAsync(pool, cancellationToken))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        var slot = 1;
        while (taken.Contains($"{pool}-{slot}"))
        {
            slot++;
        }

        return $"{pool}-{slot}";
    }

    private Task AttestAsync(
        string pool,
        string action,
        PoolObservation observed,
        ScopeProbe probe,
        Guid? actionId,
        CancellationToken cancellationToken) =>
        _protocol.AttestAsync(pool, new PoolAttestation
        {
            AttestationId = Guid.CreateVersion7(),
            Pool = pool,
            Action = action,
            ActionId = actionId,
            Outcome = observed.Outcome,
            ImageDigest = observed.ImageDigest,
            Provenance = observed.Provenance,
            ScopeProbedAt = probe.ProbedAt,
            MeasuredAt = _clock.UtcNow,
            Diagnosis = observed.Diagnosis,
        }, cancellationToken);
}
