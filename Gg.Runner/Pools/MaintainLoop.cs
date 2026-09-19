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
/// <param name="narrate">
/// Where a refusal is said out loud. Optional, and a no-op by default, because
/// most tests are not about the narration - but a real pull point passes one:
/// this loop reported NOTHING for its whole life, so hours of crash-looping
/// looked identical to hours of quietly working.
/// </param>
public sealed class MaintainLoop(
    IPoolProtocol protocol,
    IPoolAdapter adapter,
    IClock clock,
    Func<TimeSpan, CancellationToken, Task> delay,
    Action<string>? narrate = null,
    string controlPlane = "",
    // WHO TO ASK FOR MORE TIME, or nobody. Separate from the pool protocol
    // because a renewal is about this machine's credential rather than about
    // its pool - and a test about warming should not have to answer questions
    // about credentials.
    IRunnerCredential? credential = null,
    // WHEN THIS MACHINE'S CREDENTIAL ENDS. Null means unrecorded, and unrecorded
    // never asks: renewing on a guess would have every runner registered before
    // this asking on every cycle, forever.
    DateTimeOffset? credentialExpiresAt = null,
    // WHERE A NEW EXPIRY IS WRITTEN DOWN. Handed in for the reason the control
    // plane address is: the store belongs to Gg.Client, which Gg.Runner cannot
    // see. A renewal only the running process knows about dies with it - the
    // next start reads the stored identity, finds it expired, and asks for a
    // person who has nothing to do.
    Func<DateTimeOffset, Task>? credentialRenewed = null,
    // WHERE A MEMBER SHOULD ASK FOR ITS OWN ADDRESS, as this deployment spells
    // it. Handed in for the control plane address's reason, and it is the
    // difference between a member a console can reach and one that answers an
    // introduction nobody can arrive at.
    string? stunServers = null,
    // WHERE A BUILD'S RECIPE COMES FROM, AND WHAT BUILDS IT (slice forty-one).
    // Optional because a pool with no recipe never builds - and a runner
    // started without them answers a decided build with that sentence rather
    // than doing nothing, which would leave the control plane waiting forever.
    IRecipeSource? recipes = null,
    IImageBuilder? builder = null)
{
    private readonly IPoolProtocol _protocol = protocol;
    private readonly IRecipeSource? _recipes = recipes;
    private readonly IImageBuilder? _builder = builder;
    private readonly IPoolAdapter _adapter = adapter;
    private readonly IClock _clock = clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay;
    private readonly Action<string> _narrate = narrate ?? (_ => { });

    /// <summary>Where a member this loop creates should answer to.</summary>
    /// <remarks>
    /// Passed in rather than read here: which control plane a host answers to is
    /// deployment knowledge, and this loop is handed it exactly as the flight
    /// runner is.
    /// </remarks>
    private readonly string _controlPlane = controlPlane;

    /// <summary>Where a member this loop creates should ask for its own address.</summary>
    private readonly string? _stunServers = stunServers;

    /// <summary>How this machine keeps its own credential, or null for one that does not ask.</summary>
    /// <remarks>
    /// <b>Shared with the flying runner</b>, which learned this rule a slice
    /// late. A member hears "not renewable" and it is the container boundary
    /// working; anybody else hearing it is a machine whose credential really
    /// does end on a date, and the sentence is the only warning they get.
    /// </remarks>
    private readonly CredentialRenewal? _renewal = credential is null
        ? null
        : new CredentialRenewal(
            credential, clock, credentialExpiresAt, credentialRenewed,
            ends => (narrate ?? (_ => { }))(
                $"this runner's credential ends at {ends:yyyy-MM-dd HH:mm}Z and the control "
              + "plane will not extend it. Nothing here can change that."));

    /// <summary>When this machine's credential ends, as last known here.</summary>
    private DateTimeOffset? CredentialExpiresAt => _renewal?.ExpiresAt ?? credentialExpiresAt;

    /// <summary>How this machine's credential ended, once a 401 says one has.</summary>
    private CredentialEnding? _endedCredential;

    /// <summary>
    /// How close to the end is close enough to ask.
    /// </summary>
    /// <remarks>
    /// <b>Days rather than minutes, because the ask has to survive a bad
    /// week.</b> A runner that first asked an hour before its expiry would get
    /// one attempt against a control plane that might be mid-deploy; three days
    /// is room for the transient arm to do its work without the window being so
    /// wide that a renewal is really a monthly poll.
    /// </remarks>
    public static readonly TimeSpan RenewWithin = CredentialRenewal.Within;


    /// <summary>How long to wait before asking again. Zero while things are well.</summary>
    private TimeSpan _backoff = TimeSpan.Zero;

    /// <summary>How long between cycles. Injected waiting makes it a test's choice too.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long one scope probe governs, after which the loop opens a new session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because the control plane already bounds it, and this side did not
    /// know.</b> The decider refuses an outward act whose stamp is older than its
    /// ProbeFreshness - one hour, in the control plane's repository - on the
    /// grounds that such a probe governs some other session. A maintainer probed
    /// once at startup and stamped that instant forever, so an hour after it
    /// started it could never refresh again: a member that ended on its
    /// credential stayed ended, and an applied image never rolled out. Found on
    /// vmlinux001 after seven hours, three of them with a dead member.
    /// </para>
    /// <para>
    /// <b>Half of that bound</b>, which leaves the decider's own tick, a cycle's
    /// backoff and the skew between two clocks before a stamp reads as stale.
    /// Still one probe per session, never one per cycle.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(30);

    /// <summary>Runs until cancelled. 0 is a session that ended; 69 is a bound that broke.</summary>
    public async Task<int> RunAsync(string pool, CancellationToken cancellationToken)
    {
        // BEFORE ANYTHING IS ASKED OF THE PROXY. A pool that cannot pass its
        // create rule would be refused with a 403 - which is exactly what a
        // correct out-of-scope refusal looks like, and what ProbeScopeAsync
        // treats as proof the bound holds. Refusing here keeps those two apart.
        pool = PoolNaming.Require(pool);

        if (await OpenSessionAsync(pool, cancellationToken) is not { } opened)
        {
            return 69;
        }

        ScopeProbe probe = opened;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // A NEW SESSION WHEN THIS ONE HAS REACHED ITS BOUND, before anything
                // this cycle stamps. Inside the try, so a probe that cannot be asked
                // for a moment waits like any other transient failure - keeping the
                // old stamp, which the decider will then read as stale and refuse to
                // act on, which is the right answer while the bound is unproved.
                if (_clock.UtcNow - probe.ProbedAt >= SessionLength)
                {
                    if (await OpenSessionAsync(pool, cancellationToken) is not { } renewed)
                    {
                        return 69;
                    }

                    probe = renewed;
                }

                // ITS OWN CREDENTIAL FIRST, because everything below it is done
                // with that credential. A maintainer that reached its expiry used
                // to stop and wait for a person to sign in on a machine nobody
                // visits, and every environment downstream of it went cold.
                if (_renewal is not null)
                {
                    await _renewal.RenewIfDueAsync(cancellationToken);
                }

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

                // A SERVED CYCLE CLEARS IT, so an hour of health does not inherit a
                // bad minute's wait.
                _backoff = TimeSpan.Zero;
            }
            catch (InvalidOperationException refused)
            {
                // A REFUSAL IS NOT A REASON TO STOP MAINTAINING A POOL, and this
                // is the third time this loop has had to learn that. gg#144 fixed
                // the 500; the catch below is what came of it, and it takes
                // HttpRequestException only. A 400 arrives here as
                // InvalidOperationException from RunnerProtocolClient, so on
                // 2026-09-07 this process aborted four times on the pool host
                // over 180 microseconds of clock skew.
                //
                // NOT TREATED LIKE A 401, deliberately. A 401 stops because no
                // amount of waiting fixes this machine's credential. A refusal
                // may be transient - a clock a fraction out heals on the next
                // cycle - and when it is permanent, a live loop saying so every
                // cycle is more findable than a crash loop systemd keeps
                // restarting. Either way the pool stays managed or the reason
                // stays on screen.
                //
                // The diagnosis is carried whole because it names both clocks
                // and is the only thing anybody can act on.
                _backoff = TransientFailure.Next(_backoff);
                _narrate(
                    $"{refused.Message} Asking again in {_backoff.TotalSeconds:0}s.");
            }
            catch (HttpRequestException refused)
                when (refused.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // THE ONE REFUSAL THAT IS NOT WORTH WAITING FOR, and the reason
                // this catch sits above the transient one rather than inside it.
                // A 401 is this machine's credential; no deploy finishes and
                // heals it. It used to leave here unhandled, which on a pool
                // host is a stack trace in a journal with the process gone -
                // and what stops is not one runner's work but every environment
                // on the host staying warm.
                //
                // NOTHING IS ATTESTED. The broken-bound refusal above does
                // attest before exiting, because escalation reads the ledger
                // and its credential is fine - only the scope is broken. An
                // attestation travels on the credential that was just refused,
                // so the ledger is not a place this one can be said; the
                // journal is, and a pool host is a machine whose journal
                // somebody reads.
                _endedCredential = CredentialEnding.For(
                    CredentialExpiresAt, _clock.UtcNow, refused);

                _narrate(_endedCredential.Said);
                break;
            }
            catch (HttpRequestException refusal) when (TransientFailure.IsTransient(refusal))
            {
                // THE CONTROL PLANE'S PROBLEM, NOT THIS MACHINE'S. A deploy, a
                // restart, a cold start - all of them pass, and none is a reason
                // to stop maintaining a pool. This loop used to die here, and
                // systemd restarted it into the same wall six times running
                // while the pool it manages grew unattended.
                //
                // The classification and the backoff are TransientFailure's, the
                // same ones RunnerLoop reads. A copy here is what left this loop
                // behind when its twin was fixed.
                _backoff = TransientFailure.Next(_backoff);
                _narrate(TransientFailure.Diagnose(refusal, _backoff));
            }

            try
            {
                await _delay(_backoff == TimeSpan.Zero ? PollEvery : _backoff, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Cancellation is still a session that ended; a credential that was
        // taken away is not, and the code says which without anybody parsing
        // the sentence.
        return _endedCredential?.Exit ?? 0;
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

        if (string.Equals(action.Action, PoolActions.Build, StringComparison.Ordinal))
        {
            return await BuildAsync(action, cancellationToken);
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
            return await _adapter.RefreshAsync(
                pool, member, await SpecFor(pool, member, image, cancellationToken),
                cancellationToken);
        }

        if (string.Equals(action.Action, PoolActions.Reset, StringComparison.Ordinal))
        {
            var members = await _adapter.ListAsync(pool, cancellationToken);
            return members is [var first, ..]
                ? await _adapter.ResetAsync(
                    first.Name,
                    await SpecFor(pool, first.Name, image, cancellationToken),
                    cancellationToken)
                : new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = $"a reset was decided for '{pool}' and the pool has no members.",
                };
        }

        if (string.Equals(action.Action, PoolActions.Roll, StringComparison.Ordinal))
        {
            return await RollAsync(pool, image, cancellationToken);
        }

        return new PoolObservation
        {
            Outcome = PoolOutcomes.Failed,
            Diagnosis = $"'{action.Action}' is not an action this runner knows how to take.",
        };
    }

    /// <summary>
    /// Build a strategy's recipe, push it to the pool's own registry, and say
    /// what it was built from (slice forty-one).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No member is touched.</b> The digest does not become the pin here: it
    /// goes back on the attestation, the control plane writes it as a new
    /// strategy version, and the roll that follows converges the members.
    /// </para>
    /// <para>
    /// <b>The daemon's own words stay on this machine.</b> A failed step echoes
    /// the recipe's lines, and the attestation crosses to a control plane that
    /// never holds a recipe's words (rule 2) - so the detail is narrated to this
    /// host's log and the attestation carries which stage failed.
    /// </para>
    /// </remarks>
    private async Task<PoolObservation> BuildAsync(PoolAction action, CancellationToken cancellationToken)
    {
        if (_recipes is null || _builder is null)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = "a build was decided and this runner was started without a way to "
                          + "build - no recipe source or no image builder - so nothing was built.",
            };
        }

        if (action.Recipe is not { } recipe)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = "a build was decided with no recipe, so there was nothing to build.",
            };
        }

        // THE POOL'S REGISTRY IS THE IMAGE'S OWN (rule 11): the repository part of
        // the pin in force, and nowhere a recipe could name.
        var at = action.Image?.IndexOf("@sha256:", StringComparison.Ordinal) ?? -1;
        if (action.Image is not { } pin || at < 1)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = "a build was decided without a pinned image, so there is no registry "
                          + "repository to push to.",
            };
        }

        var repository = pin[..at];
        var scratch = Path.Combine(
            Path.GetTempPath(), "gg-recipe-build", Guid.NewGuid().ToString("n"));

        try
        {
            var fetch = await _recipes.FetchAsync(recipe, scratch, cancellationToken);
            if (fetch is not RecipeFetch.Fetched fetched)
            {
                return new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = fetch is RecipeFetch.Refused(var why)
                        ? why
                        : "the recipe could not be fetched.",
                };
            }

            var tag = fetched.Commit.Length > 12 ? fetched.Commit[..12] : fetched.Commit;
            var labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["gg.built-from"] = $"{recipe.Repository.Slug}@{fetched.Commit}:{recipe.Path}",
            };

            var built = await _builder.BuildAsync(
                fetched.Directory, recipe.Dockerfile ?? "Dockerfile", $"{repository}:{tag}", labels,
                cancellationToken);

            if (built is ImageBuilt.Failed(var buildDiagnosis, var buildDetail))
            {
                _narrate($"build of {recipe.Repository.Slug}@{fetched.Commit}:{recipe.Path} failed: "
                       + (buildDetail ?? buildDiagnosis));
                return new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = buildDiagnosis,
                    RecipeCommit = fetched.Commit,
                };
            }

            var pushed = await _builder.PushImageAsync(repository, tag, cancellationToken);

            if (pushed is ImagePushed.Failed(var pushDiagnosis, var pushDetail))
            {
                _narrate($"push of {repository}:{tag} failed: " + (pushDetail ?? pushDiagnosis));
                return new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = pushDiagnosis,
                    RecipeCommit = fetched.Commit,
                };
            }

            return new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                ImageDigest = ((ImagePushed.Pushed)pushed).Digest,
                RecipeCommit = fetched.Commit,
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(scratch))
                {
                    Directory.Delete(scratch, recursive: true);
                }
            }
            catch (IOException)
            {
                // A clone that will not delete is not a reason to fail a build that
                // succeeded. The operating system will get it.
            }
        }
    }

    /// <summary>
    /// Destroy and recreate every member made from something other than the
    /// pinned image, and report the first failure by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The comparison is a pin against a pin</b> — what the container says it
    /// was made FROM against what the strategy declares — and it is exact, for
    /// the reason the adapter's own converge branch states: an approximate
    /// drift check resets every sweep, forever, and that is a bill rather than
    /// a bug.
    /// </para>
    /// <para>
    /// <b>Silence is a member left alone.</b> A listing that does not say what
    /// a member was made from says nothing about whether it drifted, and
    /// destroying a warm member on a missing field is the one mistake this act
    /// can make that costs somebody something.
    /// </para>
    /// <para>
    /// <b>It stops at the first failure.</b> Ploughing on would destroy the
    /// rest of the pool while the daemon is already refusing, and the
    /// attestation can carry one diagnosis: the honest one is the first
    /// refusal, named.
    /// </para>
    /// </remarks>
    private async Task<PoolObservation> RollAsync(
        string pool, string image, CancellationToken cancellationToken)
    {
        var members = await _adapter.ListAsync(pool, cancellationToken);
        var stale = members
            // RUNNING MEMBERS ONLY. The listing asks for ?all=true, so a member
            // whose twelve-hour credential ran out is in it, stopped - and a
            // reset creates a RUNNING member, so resetting that one grows the
            // pool. Measured on vmlinux001: the first roll that ran brought a
            // spent gg-pool-ui-3 back to life and left a pool bounded at two
            // with three. A stopped member is a slot, as NextSlotAsync says
            // below, and filling a slot is refresh's - decided only inside the
            // strategy's inventory, and converged onto the pin when it is.
            .Where(m => m.Running)
            .Where(m => m.MadeFrom is { Length: > 0 } madeFrom
                     && !string.Equals(madeFrom, image, StringComparison.Ordinal))
            .ToList();

        foreach (var member in stale)
        {
            var observed = await _adapter.ResetAsync(
                member.Name,
                await SpecFor(pool, member.Name, image, cancellationToken),
                cancellationToken);

            if (!string.Equals(observed.Outcome, PoolOutcomes.Verified, StringComparison.Ordinal))
            {
                return observed with
                {
                    Diagnosis = $"'{member.Name}' could not be rolled onto {image}: "
                              + (observed.Diagnosis ?? "the adapter did not say why."),
                };
            }
        }

        // NOTHING TO DO IS DONE, NOT FAILED. A strategy applied without moving
        // the image decides a roll as well, and a roll over a pool already on
        // its pin is the proof that the image reached it - which is the answer
        // `gg pools` should show for a fleet nobody needs to think about.
        return new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
            Provenance = stale.Count == 0
                ? EnvironmentProvenance.Reused
                : EnvironmentProvenance.Fresh,
        };
    }

    /// <summary>
    /// The lowest member index not RUNNING: containers are cattle, named
    /// <c>&lt;pool&gt;-1..N</c>, and the bound on N is the decider's — a
    /// refresh is only ever decided inside the strategy's inventory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A stopped member is a slot, not an occupant.</b> This read a name
    /// that EXISTS as a name that is TAKEN, and the listing asks for
    /// <c>?all=true</c> precisely so the pool can see members that are not
    /// running — so a member that reached the end of its twelve-hour credential
    /// kept its number forever and every later refresh took the next one. A
    /// pool whose members die of old age grew a name per member per lifetime,
    /// and each corpse cost an inspect and an attestation on every sweep.
    /// </para>
    /// <para>
    /// <b>Running is what is asked, not living.</b> The adapter decides what to
    /// do with the slot once it has it — start what can come back, replace what
    /// finished — and that decision needs the container, which this does not
    /// have and must not fetch a second opinion about.
    /// </para>
    /// </remarks>
    private async Task<string> NextSlotAsync(string pool, CancellationToken cancellationToken)
    {
        var members = await _adapter.ListAsync(pool, cancellationToken);
        var running = members
            .Where(m => m.Running)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        var slot = 1;
        while (running.Contains($"{pool}-{slot}"))
        {
            slot++;
        }

        return $"{pool}-{slot}";
    }

    /// <summary>
    /// Probes the scope, and says so on the ledger when the bound does not hold.
    /// </summary>
    /// <remarks>
    /// <b>Null is a session that must not act</b>, at startup and at every bound
    /// after it alike: a bound that has stopped holding is the same fact as one
    /// that never held. The failure CROSSES before the stop - escalation reads
    /// the ledger, and a silent exit would be nothing-arrived-nothing-complained.
    /// No scope stamp: a broken probe proves nothing, and stamping it would let a
    /// refusal read as a proof.
    /// </remarks>
    private async Task<ScopeProbe?> OpenSessionAsync(
        string pool, CancellationToken cancellationToken)
    {
        var probe = await _adapter.ProbeScopeAsync(cancellationToken);
        if (probe.Held)
        {
            return probe;
        }

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

        return null;
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
            RecipeCommit = observed.RecipeCommit,
        }, cancellationToken);
    /// <summary>
    /// What this member is to be made of, including a nonce minted for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Minted per act, not per session.</b> A nonce is single-use and
    /// short-lived, so one obtained at startup would be spent or expired by the
    /// second member — and a nonce reused across members would give two
    /// containers one identity.
    /// </para>
    /// <para>
    /// <b>A refusal comes back as a null nonce</b> rather than a throw, and the
    /// adapter refuses to create anything without one. A member that cannot
    /// become anybody claims nothing, reports nothing, and is counted as warm
    /// forever, which is the 196 wearing a better image.
    /// </para>
    /// </remarks>
    private async Task<MemberSpec> SpecFor(
        string pool, string member, string image, CancellationToken cancellationToken)
    {
        var minted = await _protocol.MintMemberAsync(pool, member, cancellationToken);

        return new MemberSpec
        {
            Image = image,
            ControlPlane = _controlPlane,
            Nonce = minted?.Nonce,
            StunServers = _stunServers,
        };
    }

}
