using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// Everything the console can load. Every method is a verb.
/// </summary>
/// <remarks>
/// <para>
/// The console's data layer IS the verb layer. Each method here calls the same
/// <see cref="FlightCommands"/> method the corresponding <c>gg</c> verb calls
/// and returns the same <see cref="VerbResult"/>, so "every verb has a console
/// equivalent and every console action a verb, and both render the same
/// structured result" is true by construction rather than by discipline.
/// </para>
/// <para>
/// <b>There is no second way to get the data.</b> This type holds a
/// <see cref="FlightCommands"/> and nothing else - no HTTP client, no client,
/// no path of its own - which is asserted structurally, because a pane that
/// could fetch by a route no verb uses is a pane whose output the JSON cannot
/// reproduce.
/// </para>
/// <para>
/// Different renderers over one result type. Never a second way to get the
/// data.
/// </para>
/// </remarks>
public sealed class ConsoleData(
    FlightCommands commands,
    CredentialCommands credentials,
    TakeCommands takes,
    IdentityCommands identity,
    EnvelopeCommands envelopes)
{
    private readonly FlightCommands _commands = commands;
    private readonly CredentialCommands _credentials = credentials;
    private readonly TakeCommands _takes = takes;

    /// <summary>
    /// Who this session is, and what this tenant should know.
    /// </summary>
    /// <remarks>
    /// <b>Required rather than optional.</b> An optional read is one a
    /// composition root can forget to pass, and this console has now spent two
    /// slices finding things that were registered and never invoked - the
    /// takeover's ports were optional for exactly that reason and answered
    /// "not configured" on every real press for two slices.
    /// </remarks>
    private readonly IdentityCommands _identity = identity;

    /// <summary>Reading the envelope in force. The read, never the apply.</summary>
    private readonly EnvelopeCommands _envelopes = envelopes;

    /// <summary>
    /// `gg bundle`, from the state the console is holding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It takes the state and reads almost none of it.</b> That is the
    /// point. The console holds the live channel - whatever a runner printed,
    /// including whatever a tool echoed that it should not have - and this is
    /// the one place where a bundle could pick it up. Passing the state in and
    /// deliberately not touching <c>Live</c> or <c>Held</c> is what makes the
    /// redaction test meaningful: the needle is right there, in scope, and
    /// still does not come out.
    /// </para>
    /// <para>
    /// Static because it decides nothing that needs a control plane. Every
    /// input has already been through a verb.
    /// </para>
    /// </remarks>
    public static DiagnosticsBundle BundleFrom(
        AppState state,
        DateTimeOffset takenAt,
        EnvironmentIdentity environment,
        DoctorReport doctor,
        FlightLog? flightLog)
    {
        ArgumentNullException.ThrowIfNull(state);

        // The flight log comes from the control plane through a verb, never
        // from the console's own copy: state.FlightLog is a projection that
        // may be stale, and a bundle carrying a stale log is worse than one
        // carrying none.
        return Bundle.Build(takenAt, environment, doctor, flightLog);
    }

    /// <summary>`gg flights`.</summary>
    /// <remarks>
    /// <b>Everything, deliberately, where the verb now defaults to what is in
    /// the air.</b> A flight LIST is not the queue - it is the raw material the
    /// queue is derived from, by log entries rather than by any member of the
    /// flight - so narrowing the fetch would narrow a derivation that was never
    /// asking this question. What the console SHOWS is its own decision and is
    /// unchanged by slice fourteen; whether it should now use the state
    /// directly is a console question, and is deferred with the slice.
    /// </remarks>
    public Task<VerbResult> ListAsync(CancellationToken cancellationToken = default) =>
        _commands.ListAsync(all: true, cancellationToken);

    /// <summary>`gg plan` - the same fetch, the same checklist.</summary>
    public Task<VerbResult> PlanAsync(
        string? reference = null, CancellationToken cancellationToken = default) =>
        _commands.PlanAsync(reference, cancellationToken);



    /// <summary>
    /// `gg ground` - stop a flight that could still have been done.
    /// </summary>
    /// <remarks>
    /// The reason is required on the wire, so it is required here: a write with
    /// nothing to say for itself is not something this surface should be able to
    /// express.
    /// </remarks>
    public Task<VerbResult> GroundAsync(
        string reference, string because, CancellationToken cancellationToken = default) =>
        _commands.GroundAsync(reference, because, cancellationToken);

    /// <summary>
    /// `gg why` — why each obligation applied to a flight, or did not.
    /// </summary>
    /// <remarks>
    /// The console shows the same attribution the verb does, from the same
    /// fetch. Both are renderers: an obligation that did not attach is the thing
    /// hardest to notice, and a console that could not show it would be a
    /// surface where non-attachment is invisible again.
    /// </remarks>
    /// <summary>
    /// `gg gates` - what is waiting on a person.
    /// </summary>
    /// <remarks>
    /// The console shows the same list the verb does, from the same fetch. It cannot
    /// answer one: there is no decision path in this build, and a console pane that
    /// offered one would be the exit this step asserts does not exist.
    /// </remarks>
    public Task<VerbResult> GatesAsync(CancellationToken cancellationToken = default) =>
        _commands.GatesAsync(cancellationToken);

    /// <summary>
    /// `gg decide` - records a decision about an obligation waiting on a person.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Present for parity and wired to no key. The console's modal is step 6, and this is
    /// the data path it will use when it arrives - the same one the verb uses, so the
    /// console cannot end up with a second way to decide.
    /// </para>
    /// <para>
    /// <b>It records nothing locally.</b> Whatever the control plane answers is what the
    /// console will show: a pane that marked the obligation satisfied when a key was
    /// pressed would advance on a claim rather than on a decision, which is the failure
    /// the round-trip refusal test exists to catch.
    /// </para>
    /// </remarks>
    public Task<VerbResult> DecideAsync(
        string reference,
        string obligation,
        string outcome,
        DecisionObservations observations,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        _commands.DecideAsync(
            reference, obligation, outcome, observations, reason,
            cancellationToken: cancellationToken);

    /// <summary>`gg fly`, from whatever a person pasted.</summary>
    /// <remarks>
    /// <para>
    /// <b>This said "text only" and that was the whole gap.</b> Of the three
    /// intent kinds the console reached exactly one: <c>gg fly --uri</c> and
    /// <c>gg fly --ticket</c> had no console path, so a person looking at a work
    /// item had to leave for a shell to open a flight against it.
    /// </para>
    /// <para>
    /// The old remark said a uri intent is "a different shape with different
    /// refusals" and that offering one from a prompt would be two things behind
    /// one key. The first half is true and is why <see cref="PastedIntent"/>
    /// does the reading in one place. The second is not: it is ONE thing behind
    /// one key - open a flight against this - and a person pasting a URL has
    /// already decided which kind it is.
    /// </para>
    /// </remarks>
    public Task<VerbResult> FlyAsync(
        string pasted,
        string? repository = null,
        CancellationToken cancellationToken = default)
    {
        var read = PastedIntent.Of(pasted);

        return read.Refusal is { } refusal
            ? Task.FromException<VerbResult>(new InvalidOperationException(refusal))
            : _commands.FlyAsync(
                read.Text, read.Uri, name: null, cancellationToken,
                provider: read.Provider, id: read.Id,
                // NULL WHERE NOTHING WAS CHOSEN, never "". An empty string is
                // the console asserting a repository named nothing, which the
                // control plane refuses for a choice nobody made.
                repository: repository is { Length: > 0 } named ? named : null);
    }

    /// <summary>
    /// A flight for a work item a reader named, declared rather than parsed.
    /// </summary>
    /// <remarks>
    /// <b>No <c>PastedIntent</c> on this path, deliberately.</b> That reads what
    /// a person typed; here a reader already told us the two values, and
    /// formatting them into one string to take apart again would lose the first
    /// id containing the separator. Neither the title nor the url crosses:
    /// what a flight is CALLED is what a person types or what ingress derives.
    /// </remarks>
    public Task<VerbResult> FlyTicketAsync(
        string provider,
        string id,
        string? repository = null,
        CancellationToken cancellationToken = default) =>
        _commands.FlyAsync(
            text: null, uri: null, name: null, cancellationToken, provider: provider, id: id,
            repository: repository is { Length: > 0 } named ? named : null);

    /// <summary>What this tenant can fly against.</summary>
    /// <remarks>
    /// Not the topology - envelope names and roles - which this class used to
    /// offer beside it and which no pane ever wanted. This is the question a
    /// person browsing actually has, and the control plane has answered it the
    /// whole time.
    /// </remarks>
    public Task<VerbResult> RepositoriesAsync(CancellationToken cancellationToken = default) =>
        _commands.RepositoriesAsync(cancellationToken);

    /// <summary>
    /// The flights already opened against one work item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question a person in this product actually has</b>, before they
    /// open another one: has this been flown already, and what happened. The
    /// correlation is <c>?intent=&lt;provider&gt;#&lt;id&gt;</c>, which the
    /// control plane already parses and refuses.
    /// </para>
    /// <para>
    /// <b>A uri intent correlates through nothing, and that is said rather than
    /// shown as emptiness.</b> <c>?intent=</c> takes <c>provider#id</c> only, so
    /// a flight opened from a pasted URL is invisible to this query - and a
    /// surface that answered "no flights" would be reporting an absence it
    /// cannot actually see.
    /// </para>
    /// </remarks>
    public Task<VerbResult> FlownAsync(
        string provider, string id, CancellationToken cancellationToken = default) =>
        _commands.ListAsync(all: true, cancellationToken, intent: $"{provider}#{id}");

    /// <summary>`gg invite`.</summary>
    public Task<VerbResult> InviteAsync(CancellationToken cancellationToken = default) =>
        _commands.InviteAsync(cancellationToken);

    /// <summary>
    /// `gg credential add`.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The value is not a parameter.</b> <c>CredentialCommands</c> prompts for it,
    /// so it is never held anywhere in this project - which is what makes registering
    /// from a console safe at all, given that this model serializes itself to disk.
    /// </para>
    /// <para>
    /// <b>THE SCOPE IS.</b> It used to be hard-coded to <c>[read]</c>, so a runner
    /// that must land work needed a credential this console could not grant - and
    /// nothing said so: the person registered one, the flight ran, and the push at
    /// the end failed at the credential. A scope is a decision, and this is the only
    /// place a developer registering from their own machine can make it.
    /// </para>
    /// </remarks>
    public Task<VerbResult> AddAsync(
        string repo,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default) =>
        _credentials.AddAsync(repo, scopes, null, cancellationToken);

    public Task<VerbResult> WhyAsync(
        string reference, string? obligation = null, CancellationToken cancellationToken = default) =>
        _commands.WhyAsync(reference, obligation, cancellationToken);

    /// <summary>`gg credential list`.</summary>
    public Task<VerbResult> ListCredentialsAsync(CancellationToken cancellationToken = default) =>
        _credentials.ListCredentialsAsync(cancellationToken);

    /// <summary>
    /// `gg credential rm`.
    /// </summary>
    /// <remarks>
    /// Here because the equivalence rule reaches it, not because a pane binds
    /// a key to it yet. A store you cannot clean is a store people work
    /// around, and a console that could only add would be exactly that.
    /// </remarks>
    public Task<VerbResult> RemoveCredentialAsync(
        string credentialId, CancellationToken cancellationToken = default) =>
        _credentials.RemoveCredentialAsync(credentialId, cancellationToken);


    /// <summary>`gg log`.</summary>
    public Task<VerbResult> LogAsync(string reference, CancellationToken cancellationToken = default) =>
        _commands.LogAsync(reference, cancellationToken);

    /// <summary>One flight's story, which is what the flight pane draws.</summary>
    /// <remarks>
    /// <b>The pane's content had no producer.</b> <c>AppState.Flight</c> was set
    /// by an arm nothing reached from the console — no verb here called
    /// <c>ShowAsync</c> — so every line of the flight pane below the log came from
    /// a field production never filled.
    /// </remarks>
    public Task<VerbResult> StoryAsync(string reference, CancellationToken cancellationToken = default) =>
        _commands.ShowAsync(reference, cancellationToken);

    /// <summary>
    /// `gg whoami`, as a value.
    /// </summary>
    /// <remarks>
    /// <b>The notices are why the boot calls it.</b> <c>AppState.Notices</c> is
    /// drawn above every queue, present even when the queue is empty, and was
    /// assigned by nothing - so the degradation this console exists to surface
    /// reached it on every call and was shown to nobody. It is also the case the
    /// queue hides by construction: when check runs stop being written, every
    /// flight still runs and still leaves the queue, so the pane is at its most
    /// reassuring exactly when this is worst.
    /// </remarks>
    public Task<VerbResult> IdentityAsync(CancellationToken cancellationToken = default) =>
        _identity.ShowAsync(cancellationToken);

    /// <summary>
    /// `gg envelope show` - the rules in force.
    /// </summary>
    /// <remarks>
    /// <b>The read and not the apply.</b> Applying takes a document from a path
    /// and this console has no file argument, which is out of scope by
    /// declaration rather than by omission. Reading is what a person does when a
    /// flight is stopped by something they cannot see.
    /// </remarks>
    public Task<VerbResult> EnvelopeAsync(CancellationToken cancellationToken = default) =>
        _envelopes.ShowAsync(cancellationToken);

    /// <summary>
    /// `gg airspace show` - every name this tenant has, and its role.
    /// </summary>
    /// <remarks>
    /// <b>The topology, not the repositories.</b> Two different reads have been
    /// called "airspace" in this codebase and they answer different questions:
    /// this one is which documents govern, and <see cref="RepositoriesAsync"/>
    /// is what a flight may be opened against.
    /// </remarks>
    public Task<VerbResult> TopologyAsync(CancellationToken cancellationToken = default) =>
        _commands.AirspaceAsync(cancellationToken: cancellationToken);

    /// <summary>
    /// Every document the airspace holds, whole, in one request.
    /// </summary>
    /// <remarks>
    /// <b>What the airspace tab draws from.</b> One read fills the pane for
    /// every row, so moving the cursor makes no request - which is the rule a
    /// per-row fetch would have broken.
    /// </remarks>
    public Task<VerbResult> AirspaceDocumentsAsync(
        CancellationToken cancellationToken = default) =>
        _commands.AirspaceDocumentsAsync(cancellationToken);

    /// <summary>
    /// `gg airspace diff` - what the working copy would change, and which way.
    /// </summary>
    /// <remarks>
    /// <b>The read half of a verb whose write half stays out.</b> Pull writes
    /// files and apply submits them; both are out of the console by declaration.
    /// Diff writes nothing - it reads the tree and asks the control plane what
    /// it holds - and it is the only place direction is computed, from the
    /// comparator the door itself runs. A pane that decided for itself which
    /// way a document moved would be the second opinion ADR-0016 § 6 refused a
    /// permission model for.
    /// </remarks>
    public Task<VerbResult> EstateDiffAsync(
        string root, CancellationToken cancellationToken = default) =>
        _commands.AirspaceDiffAsync(root, cancellationToken);

    /// <summary>
    /// `gg airspace pull` - renders the estate into the working copy.
    /// </summary>
    /// <remarks>
    /// <b>The first write on this surface, and it writes only files.</b> Pull
    /// mints no version and opens no flight: it renders what the stream already
    /// holds, which is why it can be reached from a console at all while apply
    /// - one flight, one gate, one attribution per document - stays out for
    /// now. It refuses a dirty tree rather than merging, so the act a person
    /// cannot undo is the one git already guards.
    /// </remarks>
    public Task<VerbResult> PullEstateAsync(
        string root, CancellationToken cancellationToken = default) =>
        _commands.AirspacePullAsync(root, cancellationToken);

    /// <summary>
    /// `gg airspace apply` - submits every changed document, one flight each.
    /// </summary>
    /// <remarks>
    /// <b>The write that mints something, and the console asks before it.</b>
    /// One flight, one gate, one version and one attribution per changed
    /// document, in the safe order. It is the pen ADR-0016 § 1 says does not
    /// multiply when a surface does - which is why this calls the same verb the
    /// command line does rather than reaching the endpoint itself.
    /// </remarks>
    /// <param name="declareNames">
    /// Whether to declare names the topology does not hold before applying the
    /// documents that need them.
    /// </param>
    /// <remarks>
    /// <b>Not defaulted, because the console shows them and then asks.</b> The
    /// apply question lists every name it would declare and under what parent,
    /// so a `y` there is an answer to that as well - and a defaulted parameter
    /// here is how the answer would stop reaching the verb.
    /// </remarks>
    public Task<VerbResult> ApplyEstateAsync(
        string root, bool declareNames, CancellationToken cancellationToken = default) =>
        _commands.AirspaceApplyAsync(root, declareNames, cancellationToken);

    /// <summary>
    /// `gg airspace retire` - retires one name, which opens a gate.
    /// </summary>
    /// <remarks>
    /// <b>One name per call, and the caller loops.</b> Each retirement is its
    /// own flight with its own approver, so a batch verb would be inventing a
    /// transaction the control plane does not offer - and a partial failure
    /// would have nothing to report per name.
    /// </remarks>
    public Task<VerbResult> RetireNameAsync(
        string name, CancellationToken cancellationToken = default) =>
        _commands.RetireNameAsync(name, cancellationToken);

    /// <summary>`gg runners`.</summary>
    public Task<VerbResult> RunnersAsync(CancellationToken cancellationToken = default) =>
        _commands.RunnersAsync(cancellationToken);

    /// <summary>`gg environments` - the chart.</summary>
    public Task<VerbResult> EnvironmentsAsync(CancellationToken cancellationToken = default) =>
        _commands.EnvironmentsAsync(cancellationToken);

    /// <summary>`gg strategies` - what furnishes each charted name.</summary>
    public Task<VerbResult> StrategiesAsync(CancellationToken cancellationToken = default) =>
        _commands.StrategiesAsync(cancellationToken);

    /// <summary>`gg pools` - what each managed pool last attested.</summary>
    public Task<VerbResult> PoolsAsync(CancellationToken cancellationToken = default) =>
        _commands.PoolsAsync(cancellationToken);

    /// <summary>What every allowance the fleet spends from has left.</summary>
    public Task<VerbResult> AllowancesAsync(CancellationToken cancellationToken = default) =>
        _commands.AllowancesAsync(cancellationToken);

    /// <summary>Sets or clears what an allowance's owners keep back.</summary>
    /// <remarks>
    /// <b>It answers with the fleet rather than an echo</b>, which is what
    /// lets the pane update through the arm it already has.
    /// </remarks>
    public Task<VerbResult> FloorAsync(
        string allowance, double? sessionFraction, double? weekFraction,
        CancellationToken cancellationToken = default) =>
        _commands.FloorAsync(allowance, sessionFraction, weekFraction, cancellationToken);

    /// <summary>Spends a floor somebody else set, for a while, with a reason.</summary>
    public Task<VerbResult> OverrideFloorAsync(
        string allowance, int minutes, string reason,
        CancellationToken cancellationToken = default) =>
        _commands.OverrideFloorAsync(allowance, minutes, reason, cancellationToken);

    /// <summary>
    /// What a flight tried and ruled out, for the pane a person reads before
    /// taking it over.
    /// </summary>
    /// <remarks>
    /// <b>Read rather than composed, which is the whole of slice seven.</b> The
    /// console used to have no way to get a seed at all - AppState.TakeSeed was
    /// assigned nowhere outside tests - so the takeover key answered "this console
    /// is not configured to take flights over" on every real press.
    /// </remarks>
    /// <remarks>
    /// <b>A <c>VerbResult</c>, because everything the console loads is one.</b> The
    /// existing guard is right and worth conforming to rather than widening: what a
    /// pane shows has to be what <c>--json</c> would print, or the console becomes a
    /// second view with its own data path. So a seed read WITHOUT a hold comes back
    /// as a Taken with no notes - there are no hold terms to report, because reading
    /// is not taking.
    /// </remarks>
    public async Task<VerbResult> SeedAsync(
        string reference, CancellationToken cancellationToken = default) =>
        new VerbResult.Taken(
            await _takes.SeedAsync(reference, cancellationToken)
                ?? throw new FlightNotFoundException(
                    $"No flight {reference}. Run gg flights to see what is there."),
            []);

}

/// <summary>
/// Turns verb results into console state.
/// </summary>
/// <remarks>
/// <para>
/// The one place a <see cref="VerbResult"/> becomes an <see cref="AppState"/>.
/// It reads only what the verbs returned, so a pane cannot show something no
/// verb can produce - which is the same guarantee from the other end.
/// </para>
/// <para>
/// The queue is derived here rather than fetched, because <b>the queue is not
/// a flight list</b>. Its rows are flights NEEDING ME, and the conditions that
/// put one there are facts about leases and runners that <c>gg log</c> and
/// <c>gg runners</c> already return. Fetching "the queue" from an endpoint
/// would make it a server-side list that happens to be short, which is the
/// dashboard this console exists not to be.
/// </para>
/// </remarks>
public static class ConsoleProjection
{
    /// <summary>Two expiries is a pattern rather than an incident.</summary>
    public const int ExpiriesThatMeanTrouble = 2;

    /// <summary>Applies whatever a verb returned to the model.</summary>
    public static AppState Apply(AppState state, VerbResult result)
    {
        ArgumentNullException.ThrowIfNull(state);

        return result switch
        {
            VerbResult.Flight flight => state with { Flight = flight.Value, Diagnosis = null },
            // AND INTO THE MANY, keyed by the flight it is about. The single
            // field is what the live pane reads; the dictionary is what the
            // queue counts expiries in and what the detail modal renders. They
            // were filled from different places - this arm and the boot's loop -
            // so a log read on its own reached one of them and not the other.
            VerbResult.Log log => state with
            {
                FlightLog = log.Value,
                Logs = new Dictionary<string, FlightLog>(state.Logs, StringComparer.Ordinal)
                {
                    [log.Value.FlightId] = log.Value,
                },
                Diagnosis = null,
            },

            // THE FLIGHT'S STORY, which is what `gg show` now answers. The pane
            // renders sentences from it; the log beside it stays the raw record
            // the queue's rows are derived from.
            VerbResult.Story story => state with { Story = story.Value, Diagnosis = null },
            VerbResult.Runners runners => state with { Runners = runners.Value, Diagnosis = null },

            // THE THREE THAT ANSWER "WHAT ENVIRONMENTS DO I HAVE". The chart is
            // the list; the strategies say what furnishes each; the ledger says
            // what the pull point last attested about the pools they name. Kept
            // apart rather than joined here for the reason EstateOnThisMachine
            // keeps its two apart: joining them is a projection, and a
            // projection belongs where it can be tested without a terminal.
            VerbResult.Chart chart => state with { Chart = chart.Value, Diagnosis = null },
            VerbResult.Strategies strategies =>
                state with { Strategies = strategies.Value, Diagnosis = null },
            VerbResult.Pools pools => state with { Pools = pools.Value, Diagnosis = null },

            // THE FLEET'S, NOT THIS MACHINE'S. `gg allowance` reads the
            // transcripts on the disk it runs on and has no arm here at all,
            // deliberately: projecting one laptop's local reading into a model
            // a fleet pane renders from would draw a laptop and label it a
            // fleet.
            VerbResult.Allowances allowances =>
                state with { Allowances = allowances.Value, Diagnosis = null },
            // WHAT THIS TENANT CAN FLY AGAINST. The cursor resets because a
            // list read again may be shorter, and a cursor left past its end
            // would choose a repository that is no longer there.
            VerbResult.AirspaceRepositories repositories => state with
            {
                Repositories = repositories.Value,
                RepositorySelected = 0,
                Diagnosis = null,
            },
            // WHY THIS FLIGHT IS STOPPED, which is the question the queue's rows
            // pose and nothing here could answer.
            VerbResult.Why why => state with { Attribution = why.Value, Diagnosis = null },
            // THE RULES IN FORCE. Every flight names this document's version and
            // nothing here could show the document.
            VerbResult.EnvelopeShown envelope => state with
            {
                Envelope = envelope.Value,
                Diagnosis = null,
            },
            // WHAT THIS TENANT SHOULD KNOW, and the queue cannot say it. A
            // degradation that stops check runs being written leaves every
            // flight running, recording facts and leaving the queue - so
            // "nothing needs you" is true and useless. Assigned even when the
            // list is empty: a fixed degradation has to leave the pane on the
            // next read.
            VerbResult.Identity identity => state with
            {
                Notices = identity.Value.Notices,

                // ALREADY IN THE ANSWER. This verb is asked for the notices
                // row; the id came with it every time and was dropped, so the
                // console knew who it was and could not say.
                PrincipalId = identity.Value.PrincipalId,

                // AND WHETHER THEY ADMINISTER THE TENANT, which arrives in the
                // same answer. A hint about what a surface would be allowed to
                // show, never a permission: every route checks the principal
                // itself, and a control plane too old to say sends nothing,
                // which reads as no.
                IsAdmin = identity.Value.IsAdmin,
                Diagnosis = null,
            },
            // References, never secrets. There is nothing in a CredentialList
            // to withhold, which is why the flight pane can show it.
            VerbResult.Credentials credentials => state with
            {
                Credentials = credentials.Value,
                Diagnosis = null,
            },
            // A flight LIST is not the queue. It is the raw material the queue
            // is derived from, and nothing renders it directly.
            // NO LONGER A NO-OP. It cleared the diagnosis and dropped the list,
            // because the queue is DERIVED from flights rather than being them -
            // which is right, and left the detail under a selected row with
            // nowhere to come from but a second request. Holding the list is
            // what makes an arrow key free.
            VerbResult.Flights flights => state with
            {
                Flights = flights.Value,
                Diagnosis = null,
            },
            _ => state,
        };
    }

    /// <summary>
    /// Which flights need me, from what the verbs returned.
    /// </summary>
    /// <remarks>
    /// Thin, and honestly thin: two conditions, both of which step 3 actually
    /// produces. Credential-unresolvable joins at step 5 and is absent rather
    /// than stubbed - a row that could never appear is worse than a short
    /// queue, because it makes the queue look like it covers more than it does.
    /// </remarks>
    /// <param name="gates">
    /// What is waiting on a person, which is the reason this pane exists.
    /// </param>
    /// <remarks>
    /// <b>Gates were not among these arguments, and that is why the pane could
    /// not show the case it was named for.</b> <c>QueueReason.AwaitingDecision</c>
    /// was declared, rendered, and produced by nothing - a tenant whose flights
    /// were all waiting on somebody was told <i>nothing needs you</i>. The gates
    /// were already fetched at boot, six lines after this call, for the modal.
    /// </remarks>
    public static IReadOnlyList<QueueRow> Queue(
        FlightList flights,
        IReadOnlyDictionary<string, FlightLog> logs,
        RunnerList runners,
        GateList? gates = null)
    {
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(runners);

        var rows = new List<QueueRow>();

        // SINCE WHEN SOMEBODY HAS BEEN WAITING, per flight, and the EARLIEST of
        // them: a flight with two open gates is one row, and how long it has
        // been waiting is how long the first of them has. Keyed by the number
        // because that is what a gate names - it is what a person types.
        var waiting = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var gate in gates?.Gates ?? [])
        {
            waiting[gate.FlightNumber] =
                waiting.TryGetValue(gate.FlightNumber, out var already)
                && already <= gate.AwaitingSince
                    ? already
                    : gate.AwaitingSince;
        }

        foreach (var flight in flights.Flights)
        {
            // A DECISION FIRST, before any trouble. One row per flight, and when
            // a flight is both gated and stranded the reason shown is the one a
            // person can DO something about - the other is a diagnosis.
            //
            // AND IT NEEDS NO LOG, unlike the two below it. A gate is a fact
            // about a person waiting; the flight's log has nothing to add to it,
            // so a flight whose log failed to load is still shown as needing
            // somebody rather than silently dropped.
            if (waiting.TryGetValue(flight.FlightNumber, out var since))
            {
                rows.Add(Row(flight, QueueReason.AwaitingDecision, since));
                continue;
            }

            if (!logs.TryGetValue(flight.FlightId, out var log))
            {
                continue;
            }

            // THE CAUSE BEFORE THE SYMPTOMS. A flight whose credential will not
            // resolve is claimed, refused and handed back by every runner that
            // takes it, so it accumulates the shapes of trouble the two checks
            // below look for. Shown as "expired twice" a person goes and looks
            // at machines that are perfectly healthy; shown as what it is, they
            // run one command.
            //
            // SINCE THE FIRST, unlike the expiries below. Two expiries is a
            // PATTERN that became true at the second; a credential nobody has
            // registered became true at the first and has been true ever since,
            // and the default sort orders on how long somebody has been waiting.
            var unresolved = log.Entries
                .Where(e => e.Kind == StoryKinds.CredentialUnresolved)
                .ToList();

            if (unresolved.Count > 0)
            {
                rows.Add(Row(flight, QueueReason.CredentialUnresolved, unresolved[0].At));
                continue;
            }

            var expiries = log.Entries.Where(e => e.Kind == "lease-expired").ToList();
            if (expiries.Count >= ExpiriesThatMeanTrouble)
            {
                rows.Add(Row(flight, QueueReason.LeaseExpiredTwice, expiries[^1].At));
                continue;
            }

            // A runner that stopped heartbeating while holding this flight. The
            // control plane derives "offline"; nothing here second-guesses it.
            var stranded = runners.Runners.FirstOrDefault(
                r => r.State == RunnerStates.Offline && r.CurrentFlightId == flight.FlightId);

            if (stranded is not null)
            {
                rows.Add(Row(flight, QueueReason.RunnerOffline, stranded.LastHeartbeatAt ?? flight.CreatedAt));
            }
        }

        return QueueSort.Default.Order(rows);
    }

    private static QueueRow Row(FlightSummary flight, QueueReason reason, DateTimeOffset since) => new()
    {
        FlightId = flight.FlightId,
        FlightNumber = flight.FlightNumber,
        Name = flight.Name,
        Reason = reason,
        Since = since,
    };
}
