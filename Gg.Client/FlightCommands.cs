using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client;

/// <summary>A reference gg could not read at all.</summary>
public sealed class FlightReferenceException(string message) : Exception(message);

/// <summary>A reference that named no flight this tenant has.</summary>
public sealed class FlightNotFoundException(string message) : Exception(message);

/// <summary>An intent the contract's own rule refused.</summary>
public sealed class FlightIntentException(string message) : Exception(message);

/// <summary>No session, so there is nobody to act as.</summary>
public sealed class NotSignedInException(string message) : Exception(message);

/// <summary>
/// The flight verbs: fly, flights, show, log, runners.
/// </summary>
/// <remarks>
/// <para>
/// Every one returns a <see cref="VerbResult"/> and none of them writes
/// anything. That is what makes the console and <c>--json</c> two views of one
/// result rather than two implementations that agree today - there is no
/// second path here to write text down.
/// </para>
/// <para>
/// Two things are decided locally and only two: whether a reference is
/// readable at all, and whether an intent is well formed. Both use the
/// contract's own rule, so gg and the control plane cannot disagree about
/// them, and both save a round trip to be told something gg already knew.
/// Everything else - which flight a reference names, whether it exists, who
/// may see it - belongs to the control plane.
/// </para>
/// </remarks>
public sealed class FlightCommands(ControlPlaneClient client, ISessionStore sessions)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;

    /// <summary>The tenant's flights.</summary>
    public async Task<VerbResult> ListAsync(
        bool all = false, CancellationToken cancellationToken = default) =>
        new VerbResult.Flights(await _client.ListFlightsAsync(Session(), all, cancellationToken));

    /// <summary>One flight, by uuid or by the number a person typed.</summary>
    public async Task<VerbResult> ShowAsync(string reference, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var resolved = Readable(reference);

        return new VerbResult.Flight(
            await _client.GetFlightAsync(token, resolved, cancellationToken)
            ?? throw NoSuchFlight(reference));
    }

    /// <summary>
    /// Why each obligation applied to this flight, or did not.
    /// </summary>
    /// <remarks>
    /// <b>Fetched, never derived.</b> The attribution arrives already decided, and
    /// nothing here looks at a fact or a glob. A client that worked out why an
    /// obligation attached could explain a verdict it did not produce, and the two
    /// would drift apart quietly.
    /// </remarks>
    /// <summary>
    /// What is waiting on a person.
    /// </summary>
    /// <remarks>
    /// <b>Fetched, never derived</b>, and there is nothing here that answers a gate.
    /// The reason each gate exists is the attribution the Engine already recorded -
    /// the same value `gg why` renders - so this verb explains itself without
    /// computing anything.
    /// </remarks>
    /// <summary>
    /// Records a decision about an obligation, and renders what came back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here marks anything satisfied.</b> The obligation's state, and whether
    /// the work may now land, are both read off the response. A client that set them
    /// locally when the user pressed a key would produce a demo that works and a record
    /// that disagrees with it - Article IX in its softest clothing, which is the
    /// dangerous kind.
    /// </para>
    /// <para>
    /// <b>The manifest hash comes from the gate.</b> A decision is made against what was
    /// shown, so the hash travels with it - and a control plane whose facts have moved
    /// since refuses rather than recording an approval of something nobody saw.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> DecideAsync(
        string reference,
        string obligation,
        string outcome,
        DecisionObservations observations,
        string? reason = null,
        ObservationBound? bound = null,
        SubmitAndObserve? loop = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var rejecting = string.Equals(outcome, DecisionOutcomes.Rejected, StringComparison.Ordinal);

        if (rejecting && reason is not { Length: > 0 })
        {
            // A rejection with no reason is work sent back with nothing to act on, and
            // the loop would run again against the same instructions it just followed.
            throw new DecisionRefusedException(
                "Rejecting needs a reason. The loop runs again with it, and a rejection that says "
              + "nothing sends the work back to be done the same way.");
        }

        if (reason is { Length: > DecisionReasons.MaxLength })
        {
            // REFUSED, NEVER TRUNCATED. A reason cut in half is a different reason rather
            // than a shorter one - the rule every inline item follows.
            throw new DecisionRefusedException(
                $"That reason is {reason.Length} characters and the limit is "
              + $"{DecisionReasons.MaxLength}. It is refused rather than trimmed, because half a "
              + "reason is a different reason.");
        }

        // STRIPPED HERE, before the digest and before it leaves this machine. Trusting
        // the author does not make the bytes clean, and this text reaches a terminal.
        var clean = reason is { Length: > 0 } ? ControlText.Strip(reason, allowLineBreaks: true) : null;

        if (!DecisionOutcomes.All.Contains(outcome, StringComparer.Ordinal))
        {
            // REJECT LANDS HERE, deliberately. It is absent rather than unimplemented:
            // a verb that accepted it and returned success would record a decision
            // nobody acted on, and the flight would read as answered.
            throw new DecisionRefusedException(
                $"'{outcome}' is not a decision this version of gg can record. It knows: "
              + string.Join(", ", DecisionOutcomes.All)
              + ". Rejecting a gate is not built yet - the flight stays waiting, which is what "
              + "it already does, and nothing here will pretend otherwise.");
        }

        var token = Session();
        var resolved = Readable(reference);

        // The gate is what says which fact set this decision is about. Fetched rather
        // than guessed, and a flight with no open gate for this obligation is a refusal
        // rather than a decision recorded against nothing.
        var gate = (await _client.GatesAsync(token, cancellationToken)).Gates
            .FirstOrDefault(g =>
                string.Equals(g.ObligationId, obligation, StringComparison.Ordinal)
                && string.Equals(g.FlightNumber, resolved, StringComparison.OrdinalIgnoreCase));

        if (gate is null)
        {
            throw new DecisionRefusedException(
                $"Nothing is waiting on a decision about '{obligation}' for {reference}. "
              + "`gg gates` lists what is.");
        }

        // SUBMIT, THEN OBSERVE. The control plane still answers inline and the
        // answer is still carried back - but nothing here READS it to decide what
        // happened. What happened is read from the surface a person would read,
        // which is the only source that survives the write becoming a command.
        DecisionRecorded? recorded = null;

        var observed = await (loop ?? Waiting()).RunAsync(
            async ct =>
            {
                try
                {
                    recorded = await _client.DecideAsync(
                        token,
                        resolved,
                        new DecisionRequest
                        {
                            ObligationId = obligation,
                            Outcome = outcome,
                            ManifestHash = gate.ManifestHash,
                            Observations = observations,
                            Reason = clean,
                        },
                        ct);

                    // NULL IS "ACCEPTED WITH NOTHING TO SAY" NOW, not "no such
                    // flight" - a flight that does not exist raises from the
                    // transport, because the two answers used to share a value and
                    // one of them stopped being exceptional.
                    return null;
                }
                catch (DecisionRefusedException refused)
                {
                    // AN ANSWER, so the loop stops rather than waiting for
                    // something nobody wrote. It is not a failure of the wait.
                    return refused.Message;
                }
            },
            ct => ObserveAsync(token, resolved, obligation, gate.ManifestHash, ct),
            bound ?? ObservationBound.Default,
            cancellationToken);

        return new VerbResult.Decided(new DecisionReport
        {
            Observation = observed,
            // Carried, not consulted. Step 2 empties this field and nothing above
            // it changes, which is the property this ordering exists to buy.
            Decision = recorded,
        });
    }

    /// <summary>The loop a person at a terminal gets.</summary>
    private static SubmitAndObserve Waiting() =>
        new((span, ct) => Task.Delay(span, ct), () => DateTimeOffset.UtcNow);

    /// <summary>
    /// Whether this decision is visible yet, and what it came to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The gate is the signal; the verdict is the answer.</b> Exactly one thing
    /// closes a gate and it is a decision - the control plane asserts that
    /// structurally - so a gate that was open against this manifest hash and is no
    /// longer open is a decision having been recorded. Reading the verdict instead
    /// would confuse "not decided yet" with "decided, and the obligation is still
    /// unmet for another reason".
    /// </para>
    /// <para>
    /// <b>Scoped to the manifest hash</b>, because a gate can REOPEN when the work
    /// moves. A gate that came back against a different hash is a new question,
    /// not this one still pending, and matching on the obligation alone would read
    /// it as the latter forever.
    /// </para>
    /// <para>
    /// Null means not yet, never no. That is the contract the observation loop
    /// rests on, and inverting it here is the one mistake that would turn a slow
    /// control plane into a recorded rejection.
    /// </para>
    /// </remarks>
    private async Task<string?> ObserveAsync(
        string token, string resolved, string obligation, string manifestHash,
        CancellationToken cancellationToken)
    {
        var stillOpen = (await _client.GatesAsync(token, cancellationToken)).Gates
            .Any(g => string.Equals(g.ObligationId, obligation, StringComparison.Ordinal)
                   && string.Equals(g.FlightNumber, resolved, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(g.ManifestHash, manifestHash, StringComparison.Ordinal));

        if (stillOpen)
        {
            return null;
        }

        // The consequence, read from the record rather than derived from what was
        // just posted. A client that reported its own outcome back to itself would
        // be observing nothing.
        var attribution = await _client.WhyAsync(token, resolved, cancellationToken);

        return attribution?.Obligations
            .FirstOrDefault(o => string.Equals(o.ObligationId, obligation, StringComparison.Ordinal))
            ?.Outcome;
    }

    public async Task<VerbResult> GatesAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Gates(await _client.GatesAsync(Session(), cancellationToken));

    public async Task<VerbResult> WhyAsync(
        string reference, string? obligation, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var resolved = Readable(reference);

        var attribution = await _client.WhyAsync(token, resolved, cancellationToken)
            ?? throw NoSuchFlight(reference);

        if (obligation is not { Length: > 0 })
        {
            return new VerbResult.Why(attribution);
        }

        // Narrowed, and a name nothing matches is a refusal rather than an empty
        // answer - "no obligation called that" and "it did not attach" are
        // different things and the second is the one this verb exists to show.
        var one = attribution.Obligations
            .Where(o => string.Equals(o.ObligationId, obligation, StringComparison.Ordinal))
            .ToList();

        if (one.Count == 0)
        {
            throw new InvalidOperationException(
                $"This flight's envelope declares no obligation called '{obligation}'. It declares: "
              + string.Join(", ", attribution.Obligations.Select(o => o.ObligationId))
              + ". An obligation that is absent from the envelope is a different thing from one "
              + "whose condition did not hold.");
        }

        return new VerbResult.Why(attribution with { Obligations = one });
    }

    /// <summary>A flight's log.</summary>    /// <summary>A flight's log.</summary>
    public async Task<VerbResult> LogAsync(string reference, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var resolved = Readable(reference);

        return new VerbResult.Log(
            await _client.GetFlightLogAsync(token, resolved, cancellationToken)
            ?? throw NoSuchFlight(reference));
    }

    /// <summary>The tenant's runners, as the control plane derives them.</summary>
    public async Task<VerbResult> RunnersAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Runners(await _client.ListRunnersAsync(Session(), cancellationToken));

    /// <summary>
    /// The checklist: the tenant-level plan, or one flight's when a reference
    /// is given.
    /// </summary>
    /// <remarks>
    /// <b>Fetched, never derived.</b> The satisfier is the lease matcher's own
    /// containment run control-plane-side; a client that worked out
    /// satisfiability from a runner list would be the second evaluator the
    /// design forbids, one process further out.
    /// </remarks>
    public async Task<VerbResult> PlanAsync(
        string? reference, CancellationToken cancellationToken = default)
    {
        var token = Session();

        if (reference is { Length: > 0 })
        {
            return new VerbResult.Plan(
                await _client.GetChecklistAsync(token, Readable(reference), cancellationToken)
                ?? throw NoSuchFlight(reference));
        }

        return new VerbResult.Plan(
            await _client.GetPlanAsync(token, cancellationToken)
            ?? throw new NoEnvelopeException(
                "No envelope has been applied, so there is nothing to plan against. "
              + "gg envelope apply is where the rules come from."));
    }

    /// <summary>The topology: every envelope name that exists, root included.</summary>
    public async Task<VerbResult> AirspaceAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.AirspaceTopology(await _client.GetTopologyAsync(Session(), cancellationToken));

    /// <summary>
    /// Renders the whole estate into the working copy, or refuses a dirty tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The refusal comes first, and it is not a warning.</b> Pull overwrites
    /// files with canonical renderings, which is the point — but overwriting
    /// formatting and overwriting somebody's unfinished edit are different acts,
    /// and only one is intended. This is a git repository, so it behaves like
    /// git: refuse, name the files, and let the person commit or discard.
    /// </para>
    /// <para>
    /// <b>No merge strategy, no stash, no cleverness.</b> ADR-0016's zero-magic
    /// commitment is what keeps the working copy replaceable by any forge or by
    /// none, and a tool that resolved conflicts here would be the forge becoming
    /// a pen by a different route.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> AirspacePullAsync(
        string root, CancellationToken cancellationToken = default)
    {
        if (AirspaceTree.Dirty(root) is { Count: > 0 } dirty)
        {
            throw new DirtyWorkingCopyException(dirty);
        }

        var estate = await _client.ReadEstateAsync(Session(), cancellationToken);
        return new VerbResult.AirspacePulled(AirspaceTree.Write(root, estate));
    }

    /// <summary>
    /// Applies every changed document in the working copy — one flight each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Per document, and that is ADR-0016 § 3 rather than convenience.</b> One
    /// changed file, one flight, one gate, one minted version, one attribution. A
    /// changeset spanning documents stays several applies and gains an ORDER
    /// rather than atomicity — because a gate rejected mid-changeset leaves the
    /// estate stricter than intended, which is safe, while atomicity would buy a
    /// rollback protocol across per-name streams to prevent a failure that is
    /// already safe in the only direction that matters.
    /// </para>
    /// <para>
    /// <b>Unreadable files stop the whole apply.</b> Applying the rest and saying
    /// nothing would land a partial changeset somebody believed was whole.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> AirspaceApplyAsync(
        string root, CancellationToken cancellationToken = default)
    {
        var tree = AirspaceTree.Read(root);
        if (tree.Unreadable.Count > 0)
        {
            throw new EnvelopeRefusedException(
                "The working copy holds files that sit where a document goes and do not read "
              + "as one. Nothing was applied, because applying the rest would land part of a "
              + "changeset somebody meant as a whole:\n"
              + string.Join(
                  '\n', tree.Unreadable.Select(u => $"  {u.Path}: {u.Diagnosis}")));
        }

        var estate = await _client.ReadEstateAsync(Session(), cancellationToken);
        var applied = new List<AppliedDocument>();

        // IN THE SAFE ORDER. Tightenings first, so no intermediate state is
        // looser than either endpoint - the interval between two gates is not
        // a state anybody authored, and it is where an ungoverned capability
        // would live.
        var changed = AirspaceTree.Changed(tree, estate);
        var ordered = Changeset.InSafeOrder(
        [
            .. changed.Select(d => new DocumentChange
            {
                Name = d.Name,
                Path = d.Path,
                Direction = Direction(d, estate),
            }),
        ]);

        var byName = changed.ToDictionary(d => d.Name, StringComparer.Ordinal);

        foreach (var document in ordered.Select(o => byName[o.Name]))
        {
            var answer = await _client.ApplyNamedAsync(
                Session(), document.Name, Body(document), document.BasedOn, cancellationToken);

            applied.Add(new AppliedDocument
            {
                Name = document.Name,
                Path = document.Path,
                Version = answer.Version,
                Changed = answer.Changed,
                Flight = answer.Flight,
                Awaiting = answer.Awaiting,
                Widens = answer.Widens,
            });
        }

        return new VerbResult.AirspaceApplied(new EstateApplied
        {
            Applied = applied,
            Retiring = AirspaceTree.Retiring(tree, estate),
        });
    }

    /// <summary>
    /// What the working copy would change, per document.
    /// </summary>
    /// <remarks>
    /// <b>Lines and direction, which is what decides whether an apply gates.</b>
    /// Answering in CONSEQUENCES — replaying recent flights against the proposed
    /// composition — is this slice's pre-committed cut: the apply flight is
    /// already a gate with an evidence payload, so the plan delta reaches a
    /// reviewer at the decision either way.
    /// </remarks>
    public async Task<VerbResult> AirspaceDiffAsync(
        string root, CancellationToken cancellationToken = default)
    {
        var tree = AirspaceTree.Read(root);
        var estate = await _client.ReadEstateAsync(Session(), cancellationToken);

        var held = estate.Documents.ToDictionary(d => d.Name, StringComparer.Ordinal);
        var changes = new List<DocumentChange>();

        foreach (var document in AirspaceTree.Changed(tree, estate))
        {
            // DIRECTION, from the comparator the control plane uses. It is the
            // contract's, so a person is told the same thing the door will
            // decide - never a second opinion about what tightens.
            var widening =
                document.Envelope is { } proposed
                && held.TryGetValue(document.Name, out var current)
                && current.Envelope is { } applied
                    ? EnvelopeDirection.Widening(applied, proposed)
                    : null;

            changes.Add(new DocumentChange
            {
                Name = document.Name,
                Path = document.Path,
                Direction = widening is null ? "tightening" : "widening",
                Field = widening?.Field,
                Because = widening?.Because,
            });
        }

        return new VerbResult.AirspaceDiffed(new EstateDiff
        {
            // THE ORDER A PERSON READS IS THE ORDER THAT WILL HAPPEN. A diff
            // listing changes in one order while apply ran them in another
            // would be a review of something that never occurs.
            Changes = Changeset.InSafeOrder(changes),
            Retiring = AirspaceTree.Retiring(tree, estate),
            Unreadable = [.. tree.Unreadable.Select(u => u.Path)],
        });
    }

    /// <summary>
    /// Which way a document moves, from the contract's own comparator.
    /// </summary>
    /// <remarks>
    /// <b>The same computation the door will run.</b> A second opinion about
    /// what tightens is the second source of truth about direction slice ten
    /// refused a permission model for - so this asks the comparator rather than
    /// deciding, and a document with no predecessor is a tightening because
    /// genesis constrains nothing that was there before.
    /// </remarks>
    private static string Direction(TreeDocument document, AirspaceEstate estate)
    {
        var held = estate.Documents.FirstOrDefault(
            d => string.Equals(d.Name, document.Name, StringComparison.Ordinal));

        if (held?.Envelope is not { } applied || document.Envelope is not { } proposed)
        {
            return Changeset.Tightening;
        }

        return EnvelopeDirection.Widening(applied, proposed) is null
            ? Changeset.Tightening
            : Changeset.Widening;
    }

    private static NamedEnvelopeApply Body(TreeDocument document) =>
        new() { Envelope = document.Envelope, Narrowing = document.Narrowing };

    /// <summary>Every runner's advertised labels, each with its disposition.</summary>
    public async Task<VerbResult> RunnerLabelsAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.RunnerLabels(await _client.ListRunnersAsync(Session(), cancellationToken));

    /// <summary>
    /// Invites somebody into the caller's tenant.
    /// </summary>
    /// <remarks>
    /// Nothing is asked for and nothing is validated here, because there is
    /// nothing to validate: an invitation names nobody, and the tenant comes
    /// from the session. What comes back is a link the person who ran this
    /// passes on themselves.
    /// </remarks>
    public async Task<VerbResult> InviteAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Invited(await _client.InviteAsync(Session(), cancellationToken));

    /// <summary>
    /// Opens a flight.
    /// </summary>
    /// <remarks>
    /// The intent is validated by <see cref="FlightIntent.Validate"/> - the
    /// contract's rule, not a second one written here - so a request the
    /// control plane would refuse is not sent at all.
    /// </remarks>
    public async Task<VerbResult> FlyAsync(
        string? text,
        string? uri,
        string? name = null,
        CancellationToken cancellationToken = default,
        string? provider = null,
        string? id = null)
    {
        var token = Session();

        // The kind is DERIVED from which payload arrived, here and in one
        // place, so a caller never names a kind that disagrees with what it
        // supplied - the exact mismatch Validate refuses two statements below.
        var intent = new FlightIntent
        {
            Kind = provider is { Length: > 0 } || id is { Length: > 0 }
                ? FlightIntentKinds.Ticket
                : uri is { Length: > 0 } ? FlightIntentKinds.Uri : FlightIntentKinds.Text,
            Uri = uri,
            Text = text,
            Provider = provider,
            Id = id,
        };

        if (FlightIntent.Validate(intent) is { } diagnosis)
        {
            throw new FlightIntentException(diagnosis);
        }

        var request = new FlightLaunchRequest
        {
            // The name defaults to the intent, because a person who typed one
            // sentence should not have to type it twice. It is stripped and
            // shortened control-plane-side either way.
            Name = name is { Length: > 0 } ? name : (text ?? uri ?? $"{provider}#{id}"),
            Intent = intent,
        };

        return new VerbResult.Launched(await _client.LaunchFlightAsync(token, request, cancellationToken));
    }

    /// <summary>
    /// The reference, as text the control plane will resolve.
    /// </summary>
    /// <remarks>
    /// Passed through in the form it was READ, not translated into a uuid. The
    /// control plane owns resolution; a client that turned GG-42 into an id
    /// first would need its own copy of the tenant's numbering, which is a
    /// second source of truth for the one thing a flight number exists to
    /// avoid.
    /// </remarks>
    private static string Readable(string reference) =>
        FlightRef.TryParse(reference, out var parsed)
            ? parsed.ToString()
            : throw new FlightReferenceException(
                $"'{reference}' is not a flight. Use the number, like {FlightRef.Format(42)}, or the id.");

    private string Session() =>
        _sessions.Read()?.SessionToken
        ?? throw new NotSignedInException("Not signed in. Run gg login.");

    private static FlightNotFoundException NoSuchFlight(string reference) =>
        new($"No flight {reference}. Run gg flights to see what is there.");
}
