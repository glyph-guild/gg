using Gg.Contracts;

namespace Gg.Cli;

public abstract record CliAction
{
    /// <summary>
    /// A verb that produces a structured result, and can print it either way.
    /// </summary>
    /// <remarks>
    /// An interface rather than a field on the base record, so a verb that
    /// produces no result - <c>login</c>, <c>runner up</c> - cannot be handed a
    /// <c>--json</c> that would do nothing.
    /// </remarks>
    public interface IEmitsResult
    {
        /// <summary>Print the result as JSON rather than rendering it.</summary>
        bool Json { get; }
    }

    public sealed record LaunchConsole : CliAction;

    public sealed record PrintVersion : CliAction;

    public sealed record Login : CliAction;

    public sealed record Logout : CliAction;

    public sealed record WhoAmI : CliAction;

    public sealed record RunnerUp : CliAction;

    public sealed record RunnerServe : CliAction;

    /// <summary>
    /// The platform's own tool server, spoken to over stdio by an agent this
    /// runner launched.
    /// </summary>
    /// <remarks>
    /// Not a verb anybody types. It is a re-exec target, the way
    /// <see cref="RunnerServe"/> is - the launch passes this binary's own path
    /// as the server command, so the agent's process starts it and owns its
    /// lifetime.
    /// </remarks>
    /// <param name="Sweep">Whether the server answers a sweep's nominations rather than a flight's.</param>
    public sealed record RunnerTools(bool Sweep = false) : CliAction;

    /// <summary>
    /// The tracker reader this binary serves, for one provider at one host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a verb anybody types, on the same terms as <see cref="RunnerTools"/>:
    /// <c>IntentConfiguration.Served</c> writes this command line into the
    /// agent's mcp config, and the agent's process starts it.
    /// </para>
    /// <para>
    /// <b>Everything it needs arrives here, and none of it is a secret.</b> The
    /// child is started by the AGENT, so it cannot be relied on to inherit the
    /// runner's environment - and the credential is named rather than carried,
    /// so this line is safe in a <c>ps</c> listing. Resolving that name is this
    /// process's job, which is the whole reason the reader moved in-binary.
    /// </para>
    /// </remarks>
    /// <param name="Query">A watch's query, bound for a sweep; null for a flight's reader.</param>
    public sealed record RunnerRead(
        string Provider, string Host, string? Credential, string? Query = null) : CliAction;

    /// <summary>The resident runner: pull decided pool actions, act, attest.</summary>
    public sealed record RunnerMaintain(string Pool) : CliAction;

    /// <summary>
    /// The resident runner, one noun over: pull one watch's decided sweeps, run
    /// each, attest every one.
    /// </summary>
    /// <remarks>
    /// <b>Its own role rather than a second job inside <c>runner serve</c>.</b>
    /// A sweep mints no flight and takes no lease - it is the routine tier - and
    /// a machine that sweeps is usually not the machine that flies. Sharing the
    /// process would also put a sweep's agent and a flight's agent on one
    /// wall-clock budget with nothing deciding which matters more.
    /// </remarks>
    public sealed record RunnerSweep(string Watch) : CliAction;

    /// <summary>
    /// Makes this machine's runner a service: a systemd unit or a launchd
    /// daemon, written from templates in this binary.
    /// </summary>
    /// <remarks>
    /// <b>The token is not a member, and cannot be.</b> <see cref="Enroll"/>
    /// says one is to be read - prompted, or piped on stdin - because an
    /// argument is in shell history and in <c>ps</c> before any code of ours
    /// runs, and an enrollment token registers a machine.
    /// </remarks>
    /// <param name="ControlPlane">Where the runner reports; a first install needs one.</param>
    /// <param name="Enroll">Whether to read an enrollment token.</param>
    /// <param name="User">The service user, when not the platform's default.</param>
    public sealed record ServiceInstall(
        string? ControlPlane, bool Enroll, string? User, string? AgentBinary = null) : CliAction;

    /// <summary>Removes exactly what <see cref="ServiceInstall"/> wrote.</summary>
    public sealed record ServiceUninstall : CliAction;

    /// <summary>
    /// Opens a flight. Exactly one payload: <see cref="Text"/>, <see cref="Uri"/>,
    /// or <see cref="Provider"/> and <see cref="Id"/> together.
    /// </summary>
    public sealed record Fly(
        string? Text, string? Uri, bool Json, string? Provider = null, string? Id = null,
        /// <summary>
        /// Which repositories this flight is about, in the order they were
        /// named, or empty to inherit.
        /// </summary>
        /// <remarks>
        /// <b>A list because some work needs two.</b> A kind whose procedure
        /// lives in one repository and whose subject lives in another - the
        /// rubric in one, the code it describes in the other - could name only
        /// the one it was pinned to, and its agent stopped at the missing half.
        /// The runner has cloned a list since it was written; this is the end
        /// that could not say one.
        /// <para>
        /// <b>Order is carried, not sorted.</b> They are cloned and listed to
        /// the agent in the order typed, so naming the one the work is about
        /// first says something a reader can act on.
        /// </para>
        /// </remarks>
        IReadOnlyList<string>? Repositories = null,
        /// <summary>A person is flying this one themselves, on this machine.</summary>
        /// <remarks>
        /// <b>A flag on the flight rather than a verb of its own.</b> A
        /// hand-flown flight is created, governed, gated and landed identically;
        /// the only differences are which executor runs and how the lease is
        /// obtained. A second verb would be a second way to open a flight, with
        /// its own subset of these flags and its own drift.
        /// </remarks>
        bool ByHand = false,
        /// <summary>Which machine this flight is for, by id.</summary>
        string? Runner = null,
        /// <summary>The work kind whose regime governs this flight, or null.</summary>
        /// <remarks>
        /// <b>Null inherits, and inheriting is what every flight did before this
        /// existed.</b> Naming one can only NARROW root - the contract says so -
        /// so choosing wrong grants nothing root withheld, and the control plane
        /// refuses a name its topology does not know.
        /// </remarks>
        string? WorkKind = null,
        /// <summary>Which charted environment this flight runs in, or null.</summary>
        /// <remarks>
        /// <b>A selection, not a bound.</b> It is validated against the composed
        /// envelope's <c>environments</c>, so it can only pick from what the
        /// tenant already allows - and a name nobody charted is refused pointing
        /// at the chart.
        /// </remarks>
        string? Environment = null,
        /// <summary>
        /// A person will watch this one from wherever they are.
        /// </summary>
        /// <remarks>
        /// <b>Distinct from <see cref="ByHand"/>, and the pair is worth keeping
        /// straight.</b> By hand means a person is at THIS keyboard and the
        /// agent's terminal is theirs. This means a person is somewhere else and
        /// wants to see what the flight says - the runner opens a channel and an
        /// agent still does the work.
        /// </remarks>
        bool Attended = false)
        : CliAction, IEmitsResult;

    /// <summary>
    /// The tenant's flights, optionally narrowed to one line of work.
    /// </summary>
    /// <remarks>
    /// <b>ONE token rather than two halves</b>, because there are two identifier
    /// shapes now: a work item is <c>provider#id</c> and a link is a uri. Split
    /// here, the uri form would need a second pair of members and the client a
    /// second branch - and two spellings of one filter is how they come to
    /// disagree. It is validated at the parse and passed through whole.
    /// </remarks>
    public sealed record Flights(bool Json, bool All, string? Intent = null)
        : CliAction, IEmitsResult;

    public sealed record Show(string Reference, bool Json) : CliAction, IEmitsResult;

    public sealed record Log(string Reference, bool Json) : CliAction, IEmitsResult;

    /// <summary>What a flight recorded, which is not what it said.</summary>
    public sealed record Facts(string Reference, bool Json) : CliAction, IEmitsResult;

    public sealed record Runners(bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// How every watch is doing: the executor in force, the newest report and
    /// the cost per window.
    /// </summary>
    /// <remarks>
    /// <b>A watch is the one thing here that runs with nobody watching it</b> -
    /// no lease, no flight, no queue row while it works - so asking is the only
    /// way to know it is alive. S39.6-01.
    /// </remarks>
    public sealed record Watches(bool Json) : CliAction, IEmitsResult;

    /// <summary>The chart: every environment name an envelope may select.</summary>
    /// <remarks>
    /// <b>The refusal has been pointing here since the chart shipped.</b> An
    /// envelope naming an uncharted environment is refused saying to chart it
    /// first, and until this verb existed the tool that printed the refusal
    /// could not show what was already charted.
    /// </remarks>
    public sealed record Environments(bool Json) : CliAction, IEmitsResult;

    /// <summary>Every managed pool's latest attestation.</summary>
    /// <remarks>
    /// <b>Named by the contract rather than chosen here.</b> PoolLedger's own
    /// remark calls it "what gg pools renders" - a verb declared in the wire
    /// protocol and never written.
    /// </remarks>
    public sealed record Pools(bool Json) : CliAction, IEmitsResult;

    /// <summary>Every strategy in force: what furnishes each charted environment.</summary>
    public sealed record Strategies(bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// The checklist: the tenant-level plan, or one flight's when a reference
    /// is given.
    /// </summary>
    /// <remarks>
    /// Reads facts and exercises nothing - the passive fourth beside doctor,
    /// strategy health (which does not exist yet) and the routine actions
    /// (which do now: gg runner maintain, slice twelve). What a flight opened
    /// now would need, priced against the fleet the moment somebody asks.
    /// </remarks>
    public sealed record Plan(string? Flight, bool Json) : CliAction, IEmitsResult;

    /// <summary>gg airspace show: the topology, root first.</summary>
    /// <summary>
    /// The topology, or one applied document when a name is given.
    /// </summary>
    /// <remarks>
    /// <b>The read-back that did not exist.</b> `gg envelope show` is the root
    /// document, so a work kind applied successfully could be read by nothing -
    /// and somebody who applied one concluded twice that it had failed.
    /// </remarks>
    public sealed record AirspaceShow(bool Json, string? Name) : CliAction, IEmitsResult;

    /// <summary>
    /// Renders the whole estate into the working copy.
    /// </summary>
    /// <remarks>
    /// The estate verb, and the scope half of the placement decision: what
    /// <c>airspace</c> means is "all of it", while <c>envelope</c> and
    /// <c>strategy</c> each mean one document of their own class.
    /// </remarks>
    public sealed record AirspacePull(bool Json) : CliAction, IEmitsResult;

    /// <summary>Applies every changed document in the working copy.</summary>
    /// <summary>
    /// Applies the working copy, optionally declaring the names it needs.
    /// </summary>
    /// <remarks>
    /// <b><c>DeclareNames</c> IS OPT-IN AND THE DEFAULT REFUSES.</b> A declared
    /// name cannot be quietly withdrawn - retirement is a terminal version and
    /// always rides a gate - so a typo in a filename would mint a permanent
    /// name whose removal needs an approver. Without the flag, an apply that
    /// needs a name it has not got refuses and prints the exact command per
    /// document, which is the cheap half of the same information.
    /// </remarks>
    /// <summary>
    /// Retires a name, which always opens a gate.
    /// </summary>
    /// <remarks>
    /// <b>The name alone.</b> The topology already knows its role, so asking
    /// for one would be a second place to get it wrong - and the door takes
    /// the name in the path and no body at all.
    /// </remarks>
    public sealed record AirspaceRetire(string Name, bool Json) : CliAction, IEmitsResult;

    public sealed record AirspaceApply(bool Json, bool DeclareNames)
        : CliAction, IEmitsResult;

    /// <summary>Charts an environment name, so an envelope may select it.</summary>
    /// <remarks>
    /// <b>The meaning is a flag because it is optional and the name is not.</b>
    /// Absent means the name is a claim - <c>stated</c> - and registering a
    /// meaning is what earns <c>measured</c>. A positional second argument
    /// would make "chart a name I cannot yet describe" look like a mistake.
    /// </remarks>
    public sealed record EnvironmentChart(string Name, string? Meaning, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>What the working copy would change, per document.</summary>
    public sealed record AirspaceDiff(bool Json) : CliAction, IEmitsResult;

    /// <summary>Declares a name in the topology, so a document can reach it.</summary>
    /// <remarks>
    /// <b>The parent is defaulted here rather than at the door.</b> A blank
    /// parent is legal on the wire and means "under nothing", which the control
    /// plane's own refusal for a missing parent calls unreachable by
    /// construction - so gg picks root, which is in every tenant's topology by
    /// synthesis and is therefore the one default that is always valid. It is
    /// visible in the usage line, which is where a default a person can
    /// override belongs.
    /// </remarks>
    public sealed record AirspaceName(
        string Role, string Name, string Parent, bool Json, bool Personal = false)
        : CliAction, IEmitsResult;

    /// <summary>Registers a repository, so a flight may name it.</summary>
    /// <remarks>
    /// <para>
    /// <b>Four values and no shorthand, because a registry entry is four
    /// different facts.</b> The provider is a key the registrar chose and a
    /// runner resolves to a host of its own; the id is the forge's own
    /// identifier, which flight identity resolves through and does not drift;
    /// the path is the display label an intent is matched against and may; and
    /// the name is what envelopes and flights say. The request type is explicit
    /// that none of them is read off a URI - <i>"which host a customer's
    /// credential goes to must never be a policy edit here"</i> - so splitting
    /// one argument into four would be guessing at the one that cannot be
    /// re-derived afterwards.
    /// </para>
    /// <para>
    /// <b>The optional three are here so this is not a half-door.</b>
    /// <c>--ref</c> decides whether a ticket flight has anywhere to start work,
    /// <c>--credential none</c> is what makes a <c>file://</c> mirror flyable,
    /// and <c>--narrowings</c> is what lets a repository contribute to
    /// composition. A verb reaching only the required four would register
    /// entries that certain flights still cannot use, which is the same defect
    /// one level down.
    /// </para>
    /// </remarks>
    public sealed record RepositoryRegister(
        string Name,
        string Provider,
        string Id,
        string Path,
        string? Credential,
        string? Ref,
        string? Narrowings,
        bool Json) : CliAction, IEmitsResult;

    /// <summary>What the allowance this machine spends from has left.</summary>
    /// <remarks>
    /// Local: the transcripts are on this disk and the ceilings are in this
    /// machine's own configuration, so it contacts nothing and answers on a
    /// plane.
    /// </remarks>
    public sealed record Allowance(bool Json) : CliAction, IEmitsResult;

    /// <summary>What every allowance the fleet spends from has left.</summary>
    /// <remarks>
    /// <b>Plural, and a different question from <see cref="Allowance"/>.</b>
    /// That one reads this machine's transcripts and contacts nothing; this
    /// asks the control plane what every machine reported, including machines
    /// this one has never seen.
    /// </remarks>
    public sealed record Allowances(bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// Sets or clears what an allowance's owners keep back.
    /// </summary>
    /// <remarks>
    /// <b>Percentages on the way in, fractions on the wire.</b> A person says
    /// "keep a third" as <c>--session 33</c>; the contract carries 0.33,
    /// because a fraction has one spelling and a percentage has three.
    /// </remarks>
    public sealed record AllowanceFloor(
        // `Name` rather than `Allowance`: CliAction.Allowance is the local
        // read verb, and a positional parameter of that name collides with the
        // nested type.
        string Name, int? SessionPercent, int? WeekPercent, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>Tells the fleet what this machine's own meter says.</summary>
    /// <remarks>
    /// <b>Plural, because it contacts the control plane.</b> The singular
    /// <c>gg allowance</c> reads this disk and says so; this one publishes
    /// what it read, which is the act a machine with no runner had no way to
    /// perform.
    /// </remarks>
    public sealed record AllowanceReport(bool Json) : CliAction, IEmitsResult;

    /// <summary>Spends a floor somebody else set, for a while, with a reason.</summary>
    public sealed record AllowanceOverride(
        string Name, int Minutes, string Reason, bool Json) : CliAction, IEmitsResult;

    /// <summary>Makes somebody an administrator of the tenant, or stops being one.</summary>
    /// <remarks>
    /// <b>The word is in the verb, not in a flag.</b> <c>--revoke</c> would
    /// make granting the default of a verb whose other half takes privilege
    /// away, and the shorter spelling would be the more dangerous one.
    /// </remarks>
    public sealed record Admin(string PrincipalId, bool Granted, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>Every runner's advertised labels, each with its disposition.</summary>
    public sealed record RunnerLabels(bool Json) : CliAction, IEmitsResult;

    /// <summary>Takes a runner out of the fleet. There is no undo.</summary>
    public sealed record RunnerRetire(string RunnerId, bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg tenant name</c>: what this tenant is called.</summary>
    /// <remarks>
    /// <b>The first thing anybody can change about a tenant.</b> Every tenant
    /// in existence is called "somebody's tenant" - the sign-up path takes a
    /// name and nothing supplies one - and until now there was no way to say
    /// otherwise.
    /// </remarks>
    public sealed record TenantName(string Name, bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg fleet enroll</c>: some machines may join as one profile.</summary>
    public sealed record FleetEnroll(Gg.Contracts.EnrollmentTokenRequest Request, bool Json)
        : CliAction, IEmitsResult;

    /// <summary><c>gg fleet tokens</c>: this tenant's enrollment tokens, never their secrets.</summary>
    public sealed record FleetTokens(bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg fleet revoke &lt;id&gt;</c>: a token enrolls nothing more.</summary>
    public sealed record FleetRevoke(string TokenId, bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg runner claim &lt;id&gt;</c>: this machine is mine.</summary>
    public sealed record RunnerClaim(string RunnerId, bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg runner unclaim &lt;id&gt;</c>: give it back to open.</summary>
    public sealed record RunnerUnclaim(string RunnerId, bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg runner reserve &lt;id&gt;</c>: my runner takes only my flights.</summary>
    public sealed record RunnerReserve(string RunnerId, bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg runner release &lt;id&gt;</c>: my runner takes the tenant's work again.</summary>
    public sealed record RunnerRelease(string RunnerId, bool Json) : CliAction, IEmitsResult;

    /// <summary><c>gg runner ownership &lt;id&gt; tenant|open</c>: an admin's word.</summary>
    public sealed record RunnerOwnershipSet(string RunnerId, string Ownership, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>
    /// Watch what a runner's flight is saying, from wherever you are.
    /// </summary>
    /// <remarks>
    /// <b>The verb everything in slice thirty-four was machinery for.</b> The
    /// offerer, the seal, the relay and the runner's session all existed and
    /// nothing called them - a person opening the console found a suggested
    /// <c>ssh</c> command and no way to use any of it.
    /// </remarks>
    public sealed record RunnerWatch(string RunnerId, int Lines, bool Json) : CliAction;

    /// <summary>Forgets a runner's pinned key, so the next one is trusted afresh.</summary>
    public sealed record RunnerRepin(string RunnerId, bool Json) : CliAction, IEmitsResult;

    public sealed record Invite(bool Json) : CliAction, IEmitsResult;

    public sealed record Doctor(bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// What a newer gg would take, and the command that takes it.
    /// </summary>
    /// <remarks>
    /// <b>It moves nothing.</b> gg does not download, verify, replace or roll
    /// back its own binary - <c>dotnet</c> does that for a tool, a person does
    /// it for a native install, and an image is repinned. This verb reports the
    /// install shape and names the command; <c>UpdateBoundaryTests</c> holds
    /// the runner role away from all of it, because until this shipped the
    /// filesystem was enforcing that for free.
    /// </remarks>
    public sealed record Update(bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// Registers a credential for a repository.
    /// </summary>
    /// <remarks>
    /// <b>Three facts and no value.</b> The secret is prompted for, and there
    /// is no member here that could hold one - asserted over this type's shape
    /// in <c>CredentialArgsTests</c>, so adding one fails the build. An
    /// argument would be in shell history and in <c>ps</c> output before any
    /// code of ours ran, and neither is somewhere a later fix can reach.
    /// </remarks>
    public sealed record CredentialAdd(
        string Repo, IReadOnlyList<string> Scopes, string? Identity, bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// Puts a credential this tenant registered onto one runner.
    /// </summary>
    /// <remarks>
    /// <b>Three members and no fourth, which is the whole shape of the
    /// safety.</b> A runner, a repository and whether to print json - the
    /// secret is not here and cannot be, because an argument is in shell
    /// history and in <c>ps</c> output before any code of ours has run.
    /// </remarks>
    public sealed record CredentialSend(
        string RunnerId, string Repo, bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// Puts an agent's own long-lived token onto one runner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same verb as <see cref="CredentialSend"/>, because it is the same
    /// act</b> - a credential this machine holds, or a person types with the
    /// echo off, travels over the sealed channel to a runner's own store. What
    /// differs is which locator: a repository's is derived from a slug, an
    /// agent's from the adapter key <c>GG_EXECUTOR_BINARY</c> declares.
    /// </para>
    /// <para>
    /// <b>Its own record rather than a nullable fourth member.</b>
    /// <c>CredentialSend</c>'s shape - three members and no fourth - is
    /// asserted, and this one gets the same guard in
    /// <c>AnAgentTokenCanBeSentTests</c> instead of the first growing an
    /// optional half every reader has to null-check.
    /// </para>
    /// </remarks>
    public sealed record AgentCredentialSend(
        string RunnerId, string Agent, bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// Logs a runner's agent in from here, over the channel.
    /// </summary>
    /// <remarks>
    /// <b>A verb of its own rather than a flag on <c>credential send</c></b>,
    /// because it is a different act: a send carries a value this machine
    /// holds or a person types; this makes a RUNNER run its agent's own
    /// ceremony, shows the person a URL, and carries back the code they were
    /// given - read with the echo off, never an argument. Three members, and
    /// <c>AgentLoginArgsTests</c> holds the shape.
    /// </remarks>
    public sealed record AgentLogin(
        string RunnerId, string Agent, bool Json) : CliAction, IEmitsResult;

    public sealed record CredentialList(bool Json) : CliAction, IEmitsResult;

    public sealed record CredentialRemove(string CredentialId, bool Json) : CliAction, IEmitsResult;

    /// <summary>A redacted diagnostics bundle.</summary>
    public sealed record Bundle(bool Json) : CliAction, IEmitsResult;

    /// <summary>The tenant's envelope, as canonical text.</summary>
    /// <summary>
    /// The floor, or what governs a flight of one work kind.
    /// </summary>
    /// <remarks>
    /// <b>"The rules in force" is not one answer.</b> What governs depends on
    /// the kind of flight: the floor composed with that kind's document. A
    /// tenant who applied a work kind and read this without a name found none
    /// of their own rules in it.
    /// </remarks>
    public sealed record EnvelopeShow(bool Json, string? WorkKind) : CliAction, IEmitsResult;

    /// <summary>
    /// Why each obligation applied to a flight, or did not.
    /// </summary>
    /// <remarks>
    /// Takes a flight reference, because a person asking why is holding a GG
    /// number. The obligation argument is optional: with one, the answer is
    /// narrowed to it; without, every obligation is shown - and showing all of
    /// them is the default because non-attachment is the thing that hides.
    /// </remarks>
    public sealed record Why(string Flight, string? Obligation, bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// Take a stopped flight over, or hand it back with a decision.
    /// </summary>
    /// <remarks>
    /// <b>One verb, two arms, because they are two halves of one act.</b>
    /// <c>gg take GG-42</c> claims the flight and prints what it tried and ruled
    /// out; <c>gg take GG-42 --return completed</c> gives it back. Splitting them
    /// into two verbs would let somebody return a flight they never claimed, which
    /// the control plane refuses anyway and which a person should not be invited to
    /// try.
    /// </remarks>
    public sealed record Take(
        string Reference, string? Return, string? Note, bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// What is waiting on a person.
    /// </summary>
    /// <remarks>
    /// No arguments. A gate list narrowed by approver would be the routing this step
    /// does not have, and one narrowed by flight is what `gg why` already answers.
    /// </remarks>
    public sealed record Gates(bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// What has been nominated and is waiting to become a flight.
    /// </summary>
    /// <remarks>
    /// <b>The standing rows by default, and the ended ones on asking.</b> The
    /// page reports whether it included them, because a reader cannot infer it:
    /// a page of standing rows and a page that happens to contain no declined
    /// ones look identical, and somebody reading "nothing was declined" off the
    /// second would be reading a filter rather than a fact.
    /// </remarks>
    public sealed record Board(bool Ended, bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// Answers a nomination that is waiting for somebody.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE OUTCOME IS THE ENDING THE ROW WILL CARRY, not a word this verb
    /// mints.</b> `open` and `decline` are what a person types; what travels is
    /// <c>NominationEndings.Opened</c> or <c>Declined</c>. A second pair of
    /// words would be two spellings to keep agreeing, and the day they stop is
    /// the day gg sends something no row can record.
    /// </para>
    /// <para>
    /// <b>The sentence is positional and required</b>, which is
    /// <see cref="Ground"/>'s shape and for its reason: the door refuses a
    /// blank one, so an optional flag would only move the refusal to a round
    /// trip later - and the refusal here can say what the reason is FOR.
    /// </para>
    /// <para>
    /// <b>A nomination is named by its id and never by a flight reference.</b>
    /// It has no number and may never have a flight at all, so there is no
    /// GG-42 for one - and accepting one would send a string the door answers
    /// 404 to, for a reason that reads like the row is gone.
    /// </para>
    /// </remarks>
    public sealed record BoardDecide(
        string Nomination, string Outcome, string Because, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>
    /// Records a decision about an obligation waiting on a person.
    /// </summary>
    /// <remarks>
    /// <b>The outcome is a word, not a flag pair.</b> `--approve` and `--reject` as two
    /// booleans admits a state where both or neither are set, and the answer to "what did
    /// you decide" would then have four possible readings. One word has one.
    /// </remarks>
    public sealed record Decide(
        string Flight, string Obligation, string Outcome, string? Reason, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>
    /// Stops a flight that could still have been done.
    /// </summary>
    /// <remarks>
    /// <b>The reason is positional and required, which is the shape of the
    /// verb.</b> The wire refuses a blank one, so an optional flag would only
    /// move the refusal to a round trip later - and the sentence it would
    /// refuse with is about a field, where this one can say what the reason is
    /// for. Grounding is an ending and has no resumable half, so there are no
    /// modes and one arm.
    /// </remarks>
    public sealed record Ground(string Reference, string Because, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>Writes an envelope back, from a file or from stdin.</summary>
    /// <remarks>
    /// A path or "-". Reading from stdin is what makes this composable with an
    /// editor and with the sync a customer keeping envelopes in git will want.
    /// </remarks>
    public sealed record EnvelopeApply(string Source, bool Json) : CliAction, IEmitsResult;

    /// <summary>Apply a strategy document to its topology name.</summary>
    public sealed record StrategyApply(string Name, string Source, bool Json) : CliAction, IEmitsResult;

    /// <summary>
    /// <c>gg strategy build &lt;name&gt;</c>: ask for a build of the strategy's
    /// recipe (slice forty-one). What comes back is the decision.
    /// </summary>
    public sealed record StrategyBuild(string Name, bool Json) : CliAction, IEmitsResult;

    /// <summary>Checks an envelope without contacting anything.</summary>
    public sealed record EnvelopeValidate(string Source, bool Json) : CliAction, IEmitsResult;

    /// <summary>Every setting in force, and which source answered.</summary>
    public sealed record ConfigShow(bool Json) : CliAction, IEmitsResult;

    /// <summary>Checks a configuration document. Contacts nothing.</summary>
    public sealed record ConfigValidate(string Source, bool Json) : CliAction, IEmitsResult;

    /// <summary>Writes a file seeded from what is in force.</summary>
    public sealed record ConfigInit(bool Json) : CliAction, IEmitsResult;

    /// <summary>Changes one setting, leaving the rest of the document alone.</summary>
    public sealed record ConfigSet(string Key, string Value, bool Json)
        : CliAction, IEmitsResult;

    /// <summary>What this control plane offers, against what is in force.</summary>
    /// <remarks>
    /// <b>The first config verb that contacts anything.</b> Its four siblings
    /// are facts about this machine and work on a plane; an offer is a fact
    /// about somebody else's control plane, so this one needs a session.
    /// </remarks>
    public sealed record ConfigOffered(bool Json) : CliAction, IEmitsResult;

    /// <summary>Takes the offer whose version was named.</summary>
    /// <remarks>
    /// <b>The version is required, and that is the safety property.</b> A bare
    /// <c>accept</c> would be consent to whatever arrives: a control plane can
    /// change its offer between somebody reading one and taking it, and the
    /// whole reason a directed key may be offered is that a person saw what was
    /// being repointed.
    /// </remarks>
    public sealed record ConfigAccept(string Version, bool Json)
        : CliAction, IEmitsResult;

    public sealed record Unknown(string Message) : CliAction;
}

public static class CliArgs
{
    /// <summary>
    /// The ending a board word means, or null when it means none of them.
    /// </summary>
    /// <remarks>
    /// <b>ONE VOCABULARY, TRANSLATED ONCE.</b> A person types the verb they
    /// would say out loud and the wire carries the ending the row will record.
    /// Mapping it here rather than accepting the ending directly keeps
    /// <c>gg board superseded &lt;id&gt;</c> from being typeable at all: the
    /// words this answers to are the two a person can cause, and the other four
    /// have no spelling on this side.
    /// </remarks>
    private static string? BoardOutcome(string word) => word switch
    {
        "open" => NominationEndings.Opened,
        "decline" => NominationEndings.Declined,
        _ => null,
    };

    /// <summary>
    /// What gg actually does today.
    /// </summary>
    /// <remarks>
    /// A usage string is a promise. Verbs join this list when they work, and
    /// not before: an unimplemented verb that reports success is Article XI's
    /// failure mode wearing a CLI - the flight fails much later, for a reason
    /// nobody can trace back to here.
    ///
    /// <c>credential add</c> joined at step 5 and <c>bundle</c> at step 9,
    /// which is what the list is for. Note what it does NOT offer: there is no
    /// way to pass a credential value on the command line, and the flag
    /// scanner in the CLI tests fails the build if one appears.
    /// </remarks>
    private static readonly string[] Verbs =
    [
        "gg                             the console",
        "gg fly <text>|--uri <uri>|--ticket <provider>#<id>  open a flight",
        "  --repo <name>                which repository it is about, by its registered name",
        "  --runner <id>                open it for one machine",
        "  --work-kind <name>           which work kind's rules govern it",
        "  --environment <name>         which charted environment it runs in",
        "  --attended                   and watch it from wherever you are",
        "gg flights [--all] [--intent <provider>#<id>|<uri>]  flights in the air, or every one",
        "gg show <flight>               one flight, by GG-42 or by id",
        "gg log <flight>                a flight's log",
        "gg facts <flight>              what a flight recorded, and which budget held it",
        "gg runners                     the runners this tenant has",
        // BESIDE RUNNERS, because it answers the same question about the other
        // kind of machine work: a runner reports by beating, and a watch
        // reports by sweeping. Nobody is watching a sweep while it runs, so
        // asking is the only way to know one is alive.
        "gg watches                     how each watch is doing: executor, last report, cost",
        "gg plan [flight]               what must hold before a flight can start",
        "gg gates                       flights stopped, waiting on somebody",
        // BESIDE GATES, because it is the same question one noun earlier: what
        // is waiting on a person. A gate is a flight that has stopped; a
        // standing nomination is work that has not started.
        "gg board [--all]               what has been nominated and needs somebody",
        "gg board open <id> <why>       turn a standing nomination into a flight",
        "gg board decline <id> <why>    say it is not going to be one",
        "gg why <flight> [obligation]   why a flight is stopped, and what would open it",
        // <outcome> rather than the two words it takes, because spelling them
        // here trips the guard that forbids advertising a `gg approve` verb
        // that does not exist. `gg decide` with the wrong arguments prints the
        // full form, which is where somebody typing it will be anyway.
        "gg decide <flight> <obligation> <outcome> [reason]  open a gate, or refuse it",
        // WHAT IT IS NOT is half the line's job. `withdraw` is the sentence a
        // person reaches for when a flight will not move, and it says the work
        // stopped mattering - which is usually not what happened.
        "gg ground <flight> <why>          stop a flight that could still have been done",
        "gg take <flight> [--return <outcome> [--note <note>]]  take a flight over, and hand it back",
        "gg runner labels               what each runner advertises, with its disposition",
        "gg runner retire <id>          take a runner out of the fleet, for good",
        "gg tenant name <name>          what this tenant is called; only somebody who",
        "                                 administers it may say",
        "gg fleet enroll --profile <name> --uses <n> --expires <duration> [--tenant | --claim [--reserve]]",
        "                                 a token some machines may join with, shown once",
        "gg fleet tokens                this tenant's enrollment tokens, without their secrets",
        "gg fleet revoke <id>           an enrollment token enrolls nothing more",
        "gg runner claim|unclaim <id>   make a machine yours, or give it back",
        "gg runner reserve|release <id> keep your machine to your own flights, or not",
        "gg runner ownership <id> tenant|open  an admin's word: nobody's to claim, or anybody's",
        "gg runner watch <id>           watch it, and whatever it flies next",
        "gg runner repin <id>           trust a runner's key again after it changed",
        "gg invite                      a link that makes somebody a second principal here",
        "gg credential add --repo <slug>  register a credential (the value is prompted for)",
        "gg credential send --runner <id> --repo <slug>|--agent <name>",
        "                                 put one on a machine that cannot be reached any other way",
        "gg credential list             the references the control plane holds",
        "gg credential rm <id>          forget one, here and there",
        "gg agent login --runner <id> [--agent <name>]",
        "                                 log a runner's agent in from here: it runs the ceremony,",
        "                                 you visit the URL and bring back the code",
        // WAS "the repositories this tenant has registered", which describes
        // neither `show` (the topology - envelope names and their roles) nor
        // the working-copy verbs beside it. Repositories are a different read
        // and the console is its only caller.
        "gg airspace show|pull|diff|apply  its names, and its working copy",
        "    apply --declare-names        declare names it needs, under root, first",
        "gg airspace retire <name>      retire a name - always opens a gate",
        "gg airspace name <role> <name> [--under <parent>]  declare a name a document can reach",
        "gg airspace name watch <name> --personal  declare a watch that is yours alone; its gate is yours",
        "gg airspace repositories add --name <n> --provider <p> --id <id> --path <path>",
        "    [--ref <ref>] [--credential required|none] [--narrowings <dir>]",
        "                               make a repository nameable - a new one rides a gate",
        "gg envelope show               the rules governing this tenant's flights",
        "gg strategy apply <name> <file>  manage a pool under the named strategy",
        "gg strategy build <name>       build the strategy's recipe; the pin moves when it lands",
        "gg environment chart <name> [--means <predicate>]",
        "                               chart one, so an envelope may select it - rides a gate",
        "gg environments                every environment name an envelope may select",
        "gg strategies                  what furnishes each of them, and inside which bounds",
        "gg pools                       what each managed pool last attested, and when",
        "gg envelope apply <file>|-     write them back",
        "gg envelope validate <file>|-  check a file without sending it anywhere",
        "gg config show                 every setting, and where its value came from",
        "gg config init                 write a file seeded from what is in force",
        "gg config set <key> <value>    change one setting",
        "gg config validate <file>|-    check a configuration without applying it",
        "gg config offered              what your control plane offers this machine",
        "gg config accept <version>     take the offer you just read, by version",
        "gg allowance                   what the allowance this machine spends from has left",
        "gg allowances                  what every allowance in the fleet has left",
        "gg allowances report          tell the fleet what this machine's meter says",
        "gg allowances floor <name> [--session <pct>] [--week <pct>] | --clear  keep a share back",
        "gg allowances override <name> --minutes <n> --reason <why>  spend somebody's floor",
        "gg doctor                      check what gg needs to work",
        "gg update                      whether this gg is behind, and what would move it",
        "gg bundle                      a redacted diagnostics bundle to send us",
        // One line each rather than "login | logout | whoami". A person looking
        // for a verb greps for it, and the compact form is the one spelling
        // nobody searches with.
        "gg login                       sign in on this machine",
        "gg logout                      forget the session here",
        "gg whoami                      who this machine is signed in as",
        // UNDER WHOAMI, because its argument is a principal id and whoami is
        // the only place a person can read one.
        "gg admin grant <principal>     make somebody an administrator of this tenant",
        "gg admin revoke <principal>    take it back",
        "gg runner up                   take work on this machine",
        "gg runner maintain <pool>      keep a managed pool warm, reset and attested",
        // BESIDE RUNNER UP, because it is how that runs without a terminal: the
        // platform's own service, written once, as root.
        "gg service install --control-plane <url> [--enroll] [--user <name>] [--agent-binary <path>]",
        "                               make this machine's runner a service",
        "gg service uninstall           remove exactly what service install wrote",
        "gg version                     binary, protocol and fact vocabulary",
    ];

    /// <summary>What a credential asks for when nobody says otherwise.</summary>
    /// <remarks>
    /// Read, and there is nothing else to ask for. The default is spelled out
    /// rather than implied so <c>--scopes read</c> and no flag at all are
    /// visibly the same request.
    /// </remarks>
    private static readonly string[] ReadOnly = ["read"];

    /// <summary>The value after a named option, or null.</summary>
    private static string? Value(string[] args, string option)
    {
        var at = Array.IndexOf(args, option);

        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[at + 1]
            : null;
    }

    /// <summary>
    /// The arguments with a named option AND its value removed.
    /// </summary>
    /// <remarks>
    /// <b>The pair, because dropping only the name leaves the value behind</b> -
    /// and a value left in the list is matched as a verb, which is how
    /// `gg fly --runner abc "do it"` would become a flight whose text is the
    /// runner id.
    /// </remarks>
    /// <summary>
    /// Every value a repeatable option was given, in the order they were typed.
    /// </summary>
    /// <remarks>
    /// <b>Order is carried rather than sorted.</b> A flight's repositories are
    /// cloned and listed to the agent in the order named, so naming the one the
    /// work is about first says something a reader can act on - and sorting
    /// them here would take that away with nothing gained.
    /// </remarks>
    private static IReadOnlyList<string> Values(string[] args, string option)
    {
        var found = new List<string>();

        for (var at = 0; at < args.Length; at++)
        {
            if (!string.Equals(args[at], option, StringComparison.Ordinal))
            {
                continue;
            }

            // THE SAME RULE Value USES: a value beginning with `--` is the next
            // option rather than this one's argument, so an option given
            // nothing contributes nothing - and the caller compares this count
            // against how many times the option appeared to notice.
            if (at + 1 < args.Length
                && !args[at + 1].StartsWith("--", StringComparison.Ordinal))
            {
                found.Add(args[at + 1]);
            }
        }

        return found;
    }

    /// <summary>The arguments with EVERY occurrence of an option and its value removed.</summary>
    /// <remarks>
    /// <b><see cref="Without"/> removes one.</b> A repeatable option leaves the
    /// second pair behind, and a value left in the list is matched as a verb -
    /// which is the defect that helper's own remark describes, one occurrence
    /// further along.
    /// </remarks>
    private static string[] WithoutEvery(IEnumerable<string> args, string option)
    {
        var left = args.ToArray();

        while (Array.IndexOf(left, option) >= 0)
        {
            left = Without(left, option);
        }

        return left;
    }

    private static string[] Without(IEnumerable<string> args, string option)
    {
        var kept = new List<string>();
        var skip = false;

        foreach (var arg in args)
        {
            if (skip)
            {
                skip = false;
                continue;
            }

            if (string.Equals(arg, option, StringComparison.Ordinal))
            {
                skip = true;
                continue;
            }

            kept.Add(arg);
        }

        return [.. kept];
    }

    public static CliAction Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // --json is position-independent, because a person will type it in
        // both places and being told off for one of them is not helpful.
        var json = args.Contains("--json", StringComparer.Ordinal);
        // --all is position-independent for --json's reason, and stripped the
        // same way so it cannot be mistaken for a verb.
        var all = args.Contains("--all", StringComparer.Ordinal);

        // --hand is position-independent for --json's reason: a person types a
        // trailing flag in both places. Stripped for the OTHER reason those two
        // are - an option left in the list is matched as a verb, and `fly`'s
        // arms are list patterns, so composing by stripping is one line where
        // composing by enumeration is eight arms to keep in step.
        var byHand = args.Contains("--hand", StringComparer.Ordinal);

        // --attended is stripped for --hand's reason, and --runner takes a value
        // so it is stripped as a PAIR. An option left in the list is matched as
        // a verb, and `fly`'s arms are list patterns.
        var attended = args.Contains("--attended", StringComparer.Ordinal);
        var declareNames = args.Contains("--declare-names", StringComparer.Ordinal);
        var runner = Value(args, "--runner");

        // --work-kind AND --environment TAKE VALUES, so they are stripped as
        // PAIRS for --runner's reason: dropping only the name leaves the value
        // in the list, and a value in the list is matched as a verb.
        var workKind = Value(args, "--work-kind");
        var environment = Value(args, "--environment");

        // AND --repo IS THE ONE THAT REPEATS, so it is collected rather than
        // read once, and stripped every time rather than once. A kind whose
        // procedure lives in one repository and whose subject lives in another
        // names both; the runner has cloned a list since it was written.
        var repositories = Values(args, "--repo");

        var rest = Without(
            Without(
                Without(
                    args.Where(a => a != "--json" && a != "--all" && a != "--hand"
                                 && a != "--attended" && a != "--declare-names"),
                    "--runner"),
                "--work-kind"),
            "--environment");

        // STRIPPED FOR `fly` ONLY, unlike --runner beside it. `gg credential
        // add --repo` and `gg credential send --repo` take the same flag and
        // read it from their own argument list; stripping it globally would
        // take it away from both and they would refuse for want of the thing
        // that was typed.
        var flying = rest is ["fly", ..];

        if (flying)
        {
            // A --repo GIVEN NOTHING TO BE is somebody who meant to name one.
            // Values skips it, and the strip below would remove it, so without
            // this the line reads as a flight with no repository at all - which
            // opens work against an empty tree and reports success.
            if (args.Count(a => string.Equals(a, "--repo", StringComparison.Ordinal))
                > repositories.Count)
            {
                return Unknown(
                    "gg fly --repo needs the name a repository is registered under, e.g. "
                  + "--repo payments. The console's Repositories tab lists them.");
            }

            // NAMED TWICE IS REFUSED. Cloning it twice is the cheap harm; the
            // expensive one is that two trees of one repository give an agent
            // two answers to "what does this file say" and no rule for
            // choosing between them.
            var twice = repositories
                .GroupBy(r => r, StringComparer.Ordinal)
                .FirstOrDefault(g => g.Count() > 1);

            if (twice is not null)
            {
                return Unknown(
                    $"gg fly names '{twice.Key}' more than once. Two trees of one repository "
                  + "give an agent two answers to what a file says and no rule for choosing.");
            }

            rest = WithoutEvery(rest, "--repo");
        }

        // AND REFUSED ON ANYTHING THAT IS NOT `fly`. Stripping it globally would
        // accept `gg flights --hand` and do nothing - a flag that reads as an
        // instruction and is not one, which is exactly what IEmitsResult stops
        // `--json` being. Done here rather than by a type because only one verb
        // has a hand.
        if (attended && rest is not ["fly", ..])
        {
            return Unknown(
                "--attended is a flag on `gg fly`: it says somebody will be watching the "
              + "flight. On any other verb it would read as an instruction and do nothing.");
        }

        // --runner IS TWO VERBS NOW, and they mean the same thing by it: which
        // machine. `gg fly` names the one that takes the flight and
        // `gg credential send` names the one that gets the credential, so
        // spelling it differently in the second would be a second name for a
        // fact a person already knows how to state.
        //
        // STILL REFUSED EVERYWHERE ELSE, because it is stripped globally: left
        // alone on another verb it would parse and do nothing, which is a flag
        // that reads as an instruction and is not one.
        if (runner is not null
            && rest is not ["fly", ..]
            && rest is not ["credential", "send", ..]
            && rest is not ["agent", "login", ..])
        {
            return Unknown(
                "--runner is a flag on `gg fly`, `gg credential send` and `gg agent login`: it "
              + "says which machine. On any other verb it would read as an instruction and do "
              + "nothing.");
        }

        // THE SAME RULE, AND THE SAME REASON. Stripped globally these would let
        // `gg flights --work-kind hal-score` parse and do nothing - a flag that
        // reads as an instruction and is not one.
        if ((workKind is not null || environment is not null) && rest is not ["fly", ..])
        {
            return Unknown(
                "--work-kind and --environment are flags on `gg fly`: they say which regime "
              + "governs a flight and where it runs. Only opening one takes them; on any "
              + "other verb they would read as an instruction and do nothing.");
        }

        // A TRAILING ONE IS SOMEBODY WHO MEANT TO NAME ONE, which is the shape
        // --repo already refuses by name. Value() answers null for a flag with
        // nothing after it, and falling through would open a flight whose text
        // is the word they meant as a flag.
        foreach (var named in (string[])["--work-kind", "--environment"])
        {
            if (args.Contains(named, StringComparer.Ordinal) && Value(args, named) is null)
            {
                return Unknown(
                    $"gg fly {named} needs a name after it. {named} says "
                  + (named == "--work-kind"
                        ? "which work kind's rules govern the flight - run gg airspace show "
                        + "to see the names this tenant has."
                        : "which charted environment it runs in - run gg environments to see "
                        + "what is charted."));
            }
        }

        if (byHand && rest is not ["fly", ..])
        {
            return Unknown(
                "--hand says a person is flying the flight themselves, and only gg fly opens "
              + "one. Nothing was changed.");
        }

        return rest switch
        {
            [] => new CliAction.LaunchConsole(),
            ["--version"] or ["-v"] or ["version"] => new CliAction.PrintVersion(),
            ["login"] => new CliAction.Login(),
            ["logout"] => new CliAction.Logout(),
            ["whoami"] => new CliAction.WhoAmI(),
            ["runner", "up"] => new CliAction.RunnerUp(),
            ["runner", "serve"] => new CliAction.RunnerServe(),
            ["runner", "tools"] => new CliAction.RunnerTools(),
            ["runner", "tools", Gg.Local.NominationTool.Sweep.Flag] => new CliAction.RunnerTools(Sweep: true),
            ["runner", "read", .. var read] => ReadArguments(read),
            ["runner", "maintain", var pool] => new CliAction.RunnerMaintain(pool),
            // ONE WATCH, NAMED. A sweeping runner that found its own watches
            // would claim work nobody pointed this machine at, which is the
            // derivation every pull point in the fleet is written to avoid.
            ["runner", "sweep", var watched] => new CliAction.RunnerSweep(watched),
            // AND THE REFUSAL NAMES WHAT IS MISSING. Without this arm the
            // fall-through answered "'runner' is not a gg command", which is
            // false about the verb and useless about the mistake - the shape
            // `runner retire` and `runner repin` already have their own
            // sentences for.
            ["runner", "sweep", ..] => Unknown(
                "gg runner sweep needs one watch name - the watch this machine sweeps. Run "
              + "gg watches to see which ones are in force."),
            ["runner", "labels"] => new CliAction.RunnerLabels(json),
            ["runner", "retire", var retireId] => new CliAction.RunnerRetire(retireId, json),

            // ONE ARGUMENT AND IT IS REQUIRED. `gg tenant name` alone reads as
            // a question - what is it called - and the one thing it must not
            // do is answer that by erasing the name.
            ["tenant", "name", var tenantName] => new CliAction.TenantName(tenantName, json),
            // WHOSE A MACHINE IS, AND WHAT IT TAKES (slice forty-three). The
            // control plane decides who may; this side refuses only what no
            // control plane could accept.
            // HOW A MACHINE JOINS (slice forty-three, rules 17 and 18): a person
            // decides ahead of time, and the machine redeems the decision.
            ["fleet", "enroll", .. var enrolling] => FleetEnrollArguments(enrolling, json),
            ["fleet", "tokens"] => new CliAction.FleetTokens(json),
            ["fleet", "revoke", var revokeId] => new CliAction.FleetRevoke(revokeId, json),
            ["fleet", "revoke", ..] => Unknown(
                "gg fleet revoke needs one token id. Run gg fleet tokens to see this tenant's."),
            ["fleet", ..] => Unknown("gg fleet takes enroll, tokens or revoke."),
            ["runner", "claim", var claimId] => new CliAction.RunnerClaim(claimId, json),
            ["runner", "unclaim", var unclaimId] => new CliAction.RunnerUnclaim(unclaimId, json),
            ["runner", "reserve", var reserveId] => new CliAction.RunnerReserve(reserveId, json),
            ["runner", "release", var releaseId] => new CliAction.RunnerRelease(releaseId, json),
            ["runner", "ownership", var ownedId, var ownership]
                when ownership is Gg.Contracts.RunnerOwnerships.Tenant or Gg.Contracts.RunnerOwnerships.Open =>
                new CliAction.RunnerOwnershipSet(ownedId, ownership, json),
            // CLAIMED IS A PERSON'S OWN ACT, never an admin's word about
            // somebody - so it is not a value this verb takes.
            ["runner", "ownership", _, Gg.Contracts.RunnerOwnerships.Claimed] => Unknown(
                "gg runner ownership sets tenant or open. A runner is claimed by the person it "
              + "is for, with gg runner claim <id> - never set by an admin."),
            ["runner", "ownership", ..] => Unknown(
                "gg runner ownership needs a runner id and tenant or open. Run gg runners to "
              + "see the fleet."),
            ["runner", "claim", ..] => Unknown(
                "gg runner claim needs one runner id - the machine that is yours. Run gg runners "
              + "to see the fleet."),
            ["runner", "unclaim", ..] => Unknown(
                "gg runner unclaim needs one runner id. Run gg runners to see whose each one is."),
            ["runner", "reserve", ..] => Unknown(
                "gg runner reserve needs one runner id - one of yours. Run gg runners to see "
              + "whose each one is."),
            ["runner", "release", ..] => Unknown(
                "gg runner release needs one runner id - one of yours. Run gg runners to see "
              + "whose each one is."),
            // LINES IS BOUNDED BY THE CONTRACT, not here: RunnerAskBounds.MaxLines
            // is what the runner clamps to, and a second bound on this side would
            // be a second answer to how much a person may ask for.
            ["runner", "watch", var watchId] =>
                new CliAction.RunnerWatch(watchId, 40, json),
            ["runner", "watch", var watchId, "--lines", var howMany]
                when int.TryParse(howMany, out var asked) =>
                new CliAction.RunnerWatch(watchId, asked, json),
            ["runner", "repin", var repinId] => new CliAction.RunnerRepin(repinId, json),
            ["runner", "repin", ..] => Unknown(
                "gg runner repin needs one runner id - the one whose key changed."),
            ["runner", "retire", ..] => Unknown(
                "gg runner retire needs one runner id. Run gg runners to see the fleet."),
            ["tenant", "name", ..] => Unknown(
                "gg tenant name needs the name, in one argument - gg tenant name \"Acme\". "
              + "Run gg whoami to see what this tenant is called now."),
            ["tenant", ..] => Unknown("gg tenant takes name - gg tenant name \"Acme\"."),
            ["runner", "watch"] => Unknown(
                "gg runner watch needs one runner id. Run gg runners to see the fleet - a "
              + "runner answers whenever it is beating, so it can be watched while it waits "
              + "for work."),

            // `--intent <provider>#<id>` is positional rather than pulled out
            // by the pre-scan above, and the difference is that it takes a
            // VALUE: a value-taking flag stripped position-independently is
            // how the value gets mistaken for a verb.
            ["flights", "--intent", var token] => Correlate(token, json, all),
            ["flights"] => new CliAction.Flights(json, all),
            ["flights", ..] => Unknown(
                "gg flights takes --all, --json, and --intent <provider>#<id> or a uri."),
            ["runners"] => new CliAction.Runners(json),
            // PLURAL, like runners and pools: it lists what is in force and
            // how each one is doing. `gg runner watch <id>` is a different
            // verb about a flight, and the two have never been confusable
            // because that one takes an id.
            ["watches"] => new CliAction.Watches(json),
            ["environments"] => new CliAction.Environments(json),
            // SINGULAR VERB, PLURAL LIST, the way envelope/envelopes and
            // strategy/strategies already read. The list arm is above this one
            // and matches its own word exactly, so the two cannot take each
            // other's traffic.
            ["environment", "chart", var charting, "--means", var means] =>
                new CliAction.EnvironmentChart(charting, means, json),
            ["environment", "chart", var charting] =>
                new CliAction.EnvironmentChart(charting, null, json),
            ["environment", ..] => Unknown(
                "gg environment chart <name> [--means <predicate>] charts an environment so an "
              + "envelope may select it. A name with no meaning is a claim; a meaning is what "
              + "earns `measured`. Run gg environments to see what is charted already."),
            ["pools"] => new CliAction.Pools(json),
            ["strategies"] => new CliAction.Strategies(json),
            // A NAME NARROWS IT TO ONE DOCUMENT. Without one this answers the
            // topology, exactly as it always did - adding the detail must not
            // take away the list.
            ["airspace", "show", var named] => new CliAction.AirspaceShow(json, named),
            ["airspace", "show"] => new CliAction.AirspaceShow(json, null),
            ["airspace", "pull"] => new CliAction.AirspacePull(json),
            ["airspace", "apply"] => new CliAction.AirspaceApply(json, declareNames),
            ["airspace", "diff"] => new CliAction.AirspaceDiff(json),
            // RETIRE TAKES A NAME, NOT A ROLE. The topology knows which role a
            // name has; asking for it again would be a second place to get it
            // wrong, and the door takes the name alone.
            ["airspace", "retire", var retiring] =>
                new CliAction.AirspaceRetire(retiring, json),
            ["airspace", "retire", ..] => Unknown(
                "gg airspace retire takes one name - gg airspace retire score-hall. "
              + "Retiring removes every constraint in a document at once, so it always "
              + "opens a flight and waits for whoever the document names."),

            // REGISTERING IS WHAT MAKES A REPOSITORY NAMEABLE AT ALL. The
            // ingress refuses an intent naming an unregistered one by pointing
            // at this door, and nothing here could knock on it.
            ["airspace", "repositories", "add", .. var entry] =>
                RepositoryRegister([.. entry], json),
            ["airspace", "repositories", ..] => Unknown(
                "gg airspace repositories takes add - gg airspace repositories add --name "
              + "payments --provider forge --id R_123 --path acme/payments. The registry "
              + "itself is on the console's Repositories tab."),

            // THE ASKER'S OWN WATCH. ADR-0024: a watch is the tenant's or one
            // person's, and only a watch has a person - a personal work kind or
            // narrowing would be one person's governance of everybody's work.
            ["airspace", "name", Gg.Contracts.Roles.Watch, var named, "--personal"] =>
                new CliAction.AirspaceName(
                    Gg.Contracts.Roles.Watch, named, Gg.Contracts.Roles.Root, json, Personal: true),
            ["airspace", "name", _, _, "--personal"] => Unknown(
                "--personal is for a watch and nothing else - gg airspace name watch my-queue "
              + "--personal. A watch is the tenant's or one person's; a personal work kind or "
              + "narrowing would be one person's governance of everybody's work."),
            ["airspace", "name", var role, var named, "--under", var parent] =>
                new CliAction.AirspaceName(role, named, parent, json),
            ["airspace", "name", var role, var named] =>
                new CliAction.AirspaceName(role, named, Gg.Contracts.Roles.Root, json),
            ["airspace", "name", ..] => Unknown(
                "gg airspace name takes a role and a name, in that order - "
              + "gg airspace name narrowing pci. The role is one of work-kind, narrowing, "
              + "strategy, watch or fleet-profile, and --under names the parent when it is not root."),
            ["airspace", ..] => Unknown(
                "gg airspace takes show, pull, diff, apply, name, retire or repositories."),
            ["plan"] => new CliAction.Plan(null, json),
            ["plan", var flight] => new CliAction.Plan(flight, json),
            ["invite"] => new CliAction.Invite(json),
            ["allowance"] => new CliAction.Allowance(json),
            ["allowances"] => new CliAction.Allowances(json),

            // PLURAL FOR THE FLEET, and these write to it. The singular verb
            // reads this machine's own transcripts and contacts nothing, so
            // putting a fleet write under it would make one word mean both.
            // WITHOUT A RUNNER, which is the point. An allowance is a
            // subscription and the meter measures the plan, so a machine that
            // never takes a flight can still say what is left.
            ["allowances", "report"] => new CliAction.AllowanceReport(json),

            ["allowances", "floor", var floorOf, .. var floorArgs] =>
                Floor(floorOf, floorArgs, json),
            ["allowances", "override", var spendOf, .. var spendArgs] =>
                Override(spendOf, spendArgs, json),
            // A PRINCIPAL ID, WHICH IS WHAT WHOAMI PRINTS. Not a display name
            // and not an email: two people may share either, and this changes
            // what one person may do.
            ["admin", "grant", var promoted] => new CliAction.Admin(promoted, true, json),
            ["admin", "revoke", var demoted] => new CliAction.Admin(demoted, false, json),
            ["admin", ..] => Unknown(
                "gg admin takes grant or revoke and a principal id - gg admin grant "
              + "p-7. The id is the one gg whoami prints in brackets, not a display name "
              + "or an email: two people may share either, and this changes what one "
              + "person may do."),

            ["doctor"] => new CliAction.Doctor(json),
            ["update"] => new CliAction.Update(json),
            ["service", "install", .. var installing] => ServiceInstallArguments(installing),
            ["service", "uninstall"] => new CliAction.ServiceUninstall(),
            ["service", ..] => Unknown(
                "gg service takes install or uninstall: `gg service install --control-plane "
              + "<url>` makes this machine's runner a service, and `gg service uninstall` "
              + "removes exactly what that wrote."),
            ["bundle"] => new CliAction.Bundle(json),

            ["decide", var flight, var obligation, var outcome, var reason] =>
                new CliAction.Decide(flight, obligation, outcome, reason, json),

            ["decide", var flight, var obligation, var outcome] =>
                new CliAction.Decide(flight, obligation, outcome, null, json),

            // Named arguments missing rather than guessed. "gg decide GG-42" could mean
            // any obligation, and picking one for somebody is the wrong kind of helpful
            // when the thing being picked is what they are approving.
            ["decide", ..] => new CliAction.Unknown(
                "gg decide <flight> <obligation> <approved|rejected> [reason]"),
            // BEFORE THE BARE ARM BELOW IT, because a longer match has to be
            // tried first or `gg ground GG-42 because` parses as a refusal.
            ["ground", var reference, var because] => new CliAction.Ground(reference, because, json),
            ["ground", ..] => Unknown(
                "gg ground <flight> <why>, and the why is not optional: it is the only thing "
              + "that survives to tell a later reader why work that could have been done was "
              + "not. Quote it - gg ground GG-42 \"the fleet cannot serve this yet\"."),
            ["gates"] => new CliAction.Gates(json),

            // THE LONGER ARMS FIRST, or `gg board open <id> <why>` parses as a
            // bare board listing with three stray values - the same ordering
            // `ground` states above it.
            ["board", var word, var nomination, var because]
                when BoardOutcome(word) is { } outcome =>
                    Guid.TryParse(nomination, out _)
                        ? new CliAction.BoardDecide(nomination, outcome, because, json)
                        : Unknown(
                            $"'{nomination}' is not a nomination id. A nomination has no flight "
                          + "number - having no flight yet is the whole point of one - so this "
                          + "takes the id `gg board` prints, not GG-42."),

            // NAMED ARGUMENTS MISSING RATHER THAN GUESSED. A decision with no
            // sentence is refused by the door, and the person who typed it
            // should hear that from the thing they typed it into.
            ["board", var word, ..] when BoardOutcome(word) is not null => Unknown(
                "gg board open <id> <why> and gg board decline <id> <why>, and the why is not "
              + "optional: it is the only thing that survives to tell a later reader why a "
              + "person opened work nobody had asked for, or declined work somebody had. "
              + "Quote it - gg board open 01a0792a-… \"this one blocks the release\"."),

            // A WORD A PERSON CANNOT CAUSE IS NOT A VERB HERE. Superseding is
            // the board's, withdrawal is the world's, lapsing is the clock's
            // and refusal is the rules'; a verb that took one of those would
            // let somebody record that the clock did what they did.
            ["board", var word, ..] => Unknown(
                $"'{word}' is not something a person decides about a nomination. It is open or "
              + "decline; the other endings belong to the board, the world, the clock and the "
              + "rules. `gg board` lists what is waiting."),

            // `--all` is stripped above with `--json`, so it arrives as a flag
            // rather than as a word to match - which is why there is one arm
            // here and not two.
            ["board"] => new CliAction.Board(all, json),
            ["why", var flight, var obligation] => new CliAction.Why(flight, obligation, json),
            ["why", var flight] => new CliAction.Why(flight, null, json),

            // The return arms first: a longer match has to be tried before the
            // shorter one it starts with, or `gg take GG-42 --return completed`
            // parses as a take with two stray options.
            ["take", var reference, "--return", var outcome, "--note", var note] =>
                new CliAction.Take(reference, outcome, note, json),
            ["take", var reference, "--return", var outcome] =>
                new CliAction.Take(reference, outcome, null, json),
            ["take", var reference] => new CliAction.Take(reference, null, null, json),
            ["take"] => Unknown(
                "gg take needs a flight: gg take GG-42, or the id. Add --return <outcome> to hand "
              + "it back."),
            ["why"] => Unknown(
                "gg why needs a flight: gg why GG-42, or gg why GG-42 <obligation>."),
            // A WORK KIND NARROWS IT TO WHAT ACTUALLY GOVERNS ONE. Without a
            // name this is the floor, which is what it always was - and what
            // somebody reads and mistakes for everything.
            ["envelope", "show", var kind] => new CliAction.EnvelopeShow(json, kind),
            ["envelope", "show"] => new CliAction.EnvelopeShow(json, null),
            ["envelope", "apply", var source] => new CliAction.EnvelopeApply(source, json),
            ["strategy", "apply", var name, var source] =>
                new CliAction.StrategyApply(name, source, json),
            ["strategy", "build", var name] => new CliAction.StrategyBuild(name, json),
            ["strategy", "build"] => Unknown(
                "gg strategy build needs a strategy: gg strategy build dev. A build is of one "
              + "strategy's recipe, and the pin it produces moves only that strategy."),
            ["envelope", "apply"] => Unknown(
                "gg envelope apply needs a file, or - to read the envelope from stdin."),
            ["envelope", "validate", var source] => new CliAction.EnvelopeValidate(source, json),
            ["envelope", "validate"] => Unknown(
                "gg envelope validate needs a file, or - to read the envelope from stdin."),
            ["envelope", ..] => Unknown("gg envelope takes show, apply or validate."),

            // THE FIRST FOUR CONTACT NOTHING. What this machine is configured
            // to do is a fact about this machine, so show, init, set and
            // validate work with no session and no network.
            ["config", "show"] => new CliAction.ConfigShow(json),
            ["config", "init"] => new CliAction.ConfigInit(json),
            ["config", "set", var key, var value] => new CliAction.ConfigSet(key, value, json),
            ["config", "set", ..] => Unknown(
                "gg config set needs a setting and a value, as `gg config set editor hx`."),
            ["config", "validate", var source] => new CliAction.ConfigValidate(source, json),
            ["config", "validate"] => Unknown(
                "gg config validate needs a file, or - to read one from stdin."),
            // AND THESE TWO DO, because an offer is a fact about somebody
            // else's control plane rather than about this machine.
            ["config", "offered"] => new CliAction.ConfigOffered(json),
            ["config", "accept", var version] => new CliAction.ConfigAccept(version, json),

            // THE VERSION IS REQUIRED, and the refusal says where one comes
            // from. A bare `accept` would be consent to whatever arrives, and
            // somebody who does not know the version needs telling what to run
            // rather than that an argument is missing.
            ["config", "accept", ..] => Unknown(
                "gg config accept needs the version of the offer you read, as `gg config "
              + "accept offer@7`. Run `gg config offered` to see what is offered and the "
              + "exact line to run."),
            ["config", ..] => Unknown(
                "gg config takes show, init, set, validate, offered or accept."),

            ["show", var reference] => new CliAction.Show(reference, json),
            ["log", var reference] => new CliAction.Log(reference, json),
            ["facts", var reference] => new CliAction.Facts(reference, json),

            // One payload, never two. Which one wins would otherwise be
            // decided by whoever wrote this method.
            //
            // `--ticket <provider>#<id>` is ONE argument on purpose. Two flags
            // would make the half-supplied case reachable from here, and the
            // contract already refuses that with a better sentence than an
            // argument parser can produce. This keeps the parser's job to "did
            // they type it" and leaves "is it a ticket" to the one validator.
            // `--repo <name>` is a SELECTION, not a second payload: it says
            // which registered repository the flight is about, where the intent
            // could not. A ticket names a provider and an id and no repository
            // at all, so without this a work-item flight resolves to nothing and
            // its runner materializes an empty tree.
            //
            // The registry KEY, never the forge path - a path is a display label
            // that may drift, and a flight naming the key keeps resolving after
            // somebody renames the repository on the forge.
            // `--repo` NO LONGER APPEARS IN THESE PATTERNS, because it repeats
            // and a list pattern cannot say "any number of these". It is
            // collected and stripped above, the way --runner and --work-kind
            // already were, and arrives here as a list - so one arm serves a
            // flight that names none, one, or two.
            ["fly", "--uri", var uri] => new CliAction.Fly(null, uri, json,
                Repositories: repositories, ByHand: byHand,
                Runner: runner, Attended: attended, WorkKind: workKind, Environment: environment),
            ["fly", "--ticket", var ticket] => Ticket(
                ticket, json, repositories, byHand, runner, attended, workKind, environment),

            // BEFORE the free-text arm, because that arm accepts anything. A
            // word starting with a dash is an option somebody got wrong, and
            // treating it as an intent opened a real flight called '--help' on
            // a live tenant. `fly` is the one verb whose side effect a person
            // cannot undo from here, and `--help` is what somebody types when
            // they are least sure what it does.
            //
            // The options are NAMED rather than just refused: what they were
            // asking for is exactly this list.
            ["fly", var option] when Option(option) => Unknown(
                $"'{option}' is an option, and gg fly does not have it. It takes some text, "
              + "--uri <uri>, or --ticket <provider>#<id>."),

            ["fly", var text] => new CliAction.Fly(text, null, json,
                Repositories: repositories, ByHand: byHand,
                Runner: runner, Attended: attended, WorkKind: workKind, Environment: environment),
            ["fly"] => Unknown(
                "gg fly needs something to act on: some text, --uri <uri>, "
              + "or --ticket <provider>#<id>."),
            ["fly", ..] => Unknown(
                "gg fly takes one of some text, --uri or --ticket, and this has more than one. "
              + "An intent that says two things says nothing."),

            ["agent", "login", .. var login] => AgentLogin(login, runner, json),
            ["credential", "list"] => new CliAction.CredentialList(json),
            ["credential", "rm", var credentialId] => new CliAction.CredentialRemove(credentialId, json),
            ["credential", "rm", ..] => Unknown("gg credential rm needs one credential id. Run gg credential list."),
            ["credential", "add", .. var options] => CredentialAdd(options, json),
            // THE RUNNER ARRIVES ALREADY EXTRACTED, because --runner is stripped
            // globally with its value - the same route `gg fly` takes it by. What
            // reaches here is the rest of the line.
            ["credential", "send", .. var sending] => CredentialSend(sending, runner, json),
            ["credential", ..] => Unknown("gg credential takes add, send, list or rm."),

            ["show"] => Unknown("gg show needs a flight: gg show GG-42, or the id."),
            ["log"] => Unknown("gg log needs a flight: gg log GG-42, or the id."),
            ["facts"] => Unknown(
                "gg facts needs a flight: gg facts GG-42, or the id."),

            [var verb, ..] => Unknown($"'{verb}' is not a gg command."),
            _ => Unknown("unrecognised arguments."),
        };
    }

    /// <summary>
    /// Parses the options of <c>gg credential add</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three options, all of them facts: which repository, which scopes, and
    /// which account the credential acts as. The value is prompted for and
    /// there is no option that could carry it.
    /// </para>
    /// <para>
    /// <b>An option this method does not know is refused rather than ignored.</b>
    /// Absorbing an unknown one is how a token flag would appear to work while
    /// being silently dropped - and a person who believed it did something is
    /// worse off than one who was told no.
    /// </para>
    /// </remarks>
    /// <summary>
    /// <c>provider#id</c> for a listing, split the one way it is ever split.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="SplitTicket"/> rather than repeating the rule,
    /// because this is the SECOND place a person types that token and two
    /// spellings of one rule is how they come to disagree about an id with a
    /// separator in it.
    /// </remarks>
    /// <summary>
    /// The reader's own flags, read positionally after its verb.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not pulled out by the pre-scan, and deliberately.</b> Every flag here
    /// takes a VALUE, and a value-taking flag stripped position-independently
    /// is how the value gets mistaken for a verb - the reason
    /// <c>--intent</c> is matched positionally too.
    /// </para>
    /// <para>
    /// <b>A missing host is refused rather than defaulted.</b> A reader that
    /// quietly read the wrong tracker is worse than one that does not start:
    /// the flight would get a work item, and it would be somebody else's.
    /// </para>
    /// </remarks>
    /// <summary>
    /// <c>gg service install</c>'s options, each once, and no token among them.
    /// </summary>
    /// <remarks>
    /// <b>--enroll takes no value, and a value after it is refused.</b> The
    /// token is read at a prompt or from stdin, because an argument is in shell
    /// history and in <c>ps</c> before any code of ours runs - and a word after
    /// <c>--enroll</c> is somebody pasting it into exactly that place.
    /// </remarks>
    private static CliAction ServiceInstallArguments(ReadOnlySpan<string> arguments)
    {
        string? controlPlane = null;
        string? user = null;
        string? agentBinary = null;
        var enroll = false;

        for (var at = 0; at < arguments.Length; at++)
        {
            switch (arguments[at])
            {
                case "--enroll":
                    enroll = true;
                    break;

                case "--control-plane" or "--user" or "--agent-binary"
                    when at + 1 >= arguments.Length
                      || arguments[at + 1].StartsWith("--", StringComparison.Ordinal):
                    return Unknown($"gg service install {arguments[at]} needs a value after it.");

                case "--control-plane":
                    controlPlane = arguments[++at];
                    break;

                case "--user":
                    user = arguments[++at];
                    break;

                // A PATH, NOT A NAME, and the asymmetry with `gg agent login
                // --agent <name>` is deliberate: a profile says WHICH agent by
                // name, and this says WHERE this machine's copy of it is.
                case "--agent-binary":
                    agentBinary = arguments[++at];
                    break;

                default:
                    return Unknown(
                        $"gg service install: '{arguments[at]}' is not one of its options. It takes "
                      + "--control-plane <url>, --user <name>, --agent-binary <path> and --enroll, "
                      + "which reads the enrollment token at a prompt or from stdin - never from "
                      + "the command line, where shell history and ps would keep it.");
            }
        }

        return new CliAction.ServiceInstall(controlPlane, enroll, user, agentBinary);
    }

    /// <summary>
    /// <c>gg fleet enroll</c>'s flags, in any order. Both bounds are required
    /// (rule 18): a token with no expiry or no count is a standing grant.
    /// </summary>
    private static CliAction FleetEnrollArguments(ReadOnlySpan<string> arguments, bool json)
    {
        string? profile = null;
        int? uses = null;
        TimeSpan? expires = null;
        var tenant = false;
        var claim = false;
        var reserve = false;

        for (var at = 0; at < arguments.Length; at++)
        {
            switch (arguments[at])
            {
                case "--tenant": tenant = true; continue;
                case "--claim": claim = true; continue;
                case "--reserve": reserve = true; continue;
            }

            if (at + 1 >= arguments.Length)
            {
                return Unknown($"gg fleet enroll: '{arguments[at]}' was given no value.");
            }

            switch (arguments[at])
            {
                case "--profile": profile = arguments[++at]; break;
                case "--uses" when int.TryParse(arguments[at + 1], out var n): uses = n; at++; break;
                case "--expires" when Duration(arguments[at + 1]) is { } span: expires = span; at++; break;
                case "--uses":
                    return Unknown($"gg fleet enroll: --uses is '{arguments[at + 1]}', and it takes a whole number of machines.");
                case "--expires":
                    return Unknown($"gg fleet enroll: --expires is '{arguments[at + 1]}', and it takes a duration like 30m, 24h or 7d.");
                default:
                    return Unknown(
                        $"gg fleet enroll: '{arguments[at]}' is not one of its options. It takes --profile, "
                      + "--uses, --expires, and one of --tenant or --claim (with --reserve).");
            }
        }

        if (tenant && claim)
        {
            return Unknown(
                "gg fleet enroll: --tenant and --claim are two answers to whose a machine starts as. "
              + "Say one - the tenant's, or yours.");
        }

        if (reserve && !claim)
        {
            return Unknown(
                "gg fleet enroll: --reserve keeps a machine to its owner's flights, so it comes with "
              + "--claim - a tenant or open machine has no owner to keep it for.");
        }

        return (profile, uses, expires) switch
        {
            (null or "", _, _) => Unknown(
                "gg fleet enroll needs --profile: the fleet profile every machine it enrolls runs under."),
            (_, null, _) => Unknown(
                "gg fleet enroll needs --uses: how many machines it may enroll. Nothing enrolls beyond it."),
            (_, _, null) => Unknown(
                "gg fleet enroll needs --expires, at most 7d: a token with no end is a standing grant."),
            _ => new CliAction.FleetEnroll(
                new Gg.Contracts.EnrollmentTokenRequest
                {
                    Profile = profile,
                    Uses = uses!.Value,
                    ExpiresInSeconds = (int)Math.Min(int.MaxValue, expires!.Value.TotalSeconds),
                    Ownership = tenant
                        ? Gg.Contracts.RunnerOwnerships.Tenant
                        : claim ? Gg.Contracts.RunnerOwnerships.Claimed : Gg.Contracts.RunnerOwnerships.Open,
                    Reserve = reserve,
                },
                json),
        };
    }

    /// <summary>A duration a person types: a whole number and m, h or d.</summary>
    private static TimeSpan? Duration(string text) =>
        text.Length > 1 && int.TryParse(text.AsSpan(0, text.Length - 1), out var n) && n > 0
            ? text[^1] switch
            {
                'm' => TimeSpan.FromMinutes(n),
                'h' => TimeSpan.FromHours(n),
                'd' => TimeSpan.FromDays(n),
                _ => null,
            }
            : null;

    private static CliAction ReadArguments(ReadOnlySpan<string> arguments)
    {
        string? provider = null;
        string? host = null;
        string? credential = null;
        string? query = null;

        for (var at = 0; at < arguments.Length; at += 2)
        {
            if (at + 1 >= arguments.Length)
            {
                return Unknown($"gg runner read: '{arguments[at]}' was given no value.");
            }

            switch (arguments[at])
            {
                case "--provider": provider = arguments[at + 1]; break;
                case "--host": host = arguments[at + 1]; break;
                case "--credential": credential = arguments[at + 1]; break;
                case "--query": query = arguments[at + 1]; break;
                default:
                    return Unknown(
                        $"gg runner read: '{arguments[at]}' is not one of its options. It takes "
                      + "--provider, --host, an optional --credential and, for a sweep, an "
                      + "optional --query.");
            }
        }

        return (provider, host) switch
        {
            (null or "", _) => Unknown(
                "gg runner read needs --provider: the key a flight's intent spells its tracker "
              + "with, which is also the prefix the agent sees on every tool name."),
            (_, null or "") => Unknown(
                "gg runner read needs --host: the tracker root to read from. There is no "
              + "default, because a reader pointed at the wrong tracker answers confidently "
              + "with somebody else's work."),
            _ => new CliAction.RunnerRead(provider, host, credential, query),
        };
    }

    private static CliAction Correlate(string token, bool json, bool all) =>
        SplitTicket(token) is var (provider, _) && provider is not null
        // A LINK IS THE SECOND SHAPE, and only an absolute one: `4471` and
        // `acme/widgets` are relative references TryCreate refuses, which is
        // what keeps the refusal below reachable. A parse that accepted
        // anything would turn a typo into a filter matching nothing and report
        // it as success.
        || Uri.TryCreate(token, UriKind.Absolute, out _)
            ? new CliAction.Flights(json, all, token)
            : Unknown(
                $"gg flights --intent takes <provider>#<id> or an absolute uri, and '{token}' "
              + "is neither. Both halves of a work item are needed: the id alone does not say "
              + "which tracker it is in.");

    /// <summary>
    /// The two halves of a work item token, or nulls when it is not one.
    /// </summary>
    /// <remarks>
    /// <b>Split on the FIRST separator, never on every one.</b> A tracker whose
    /// ids contain a <c>#</c> would otherwise lose the tail silently, which is
    /// the truncation failure this repository keeps finding one field at a time.
    /// </remarks>
    private static (string? Provider, string? Id) SplitTicket(string token) =>
        Gg.Client.PastedIntent.SplitTicket(token);

    /// <summary><c>provider#id</c>, split into the two fields a ticket is.</summary>
    /// <remarks>
    /// <para>
    /// <b>Split on the FIRST separator, never on every one.</b> A tracker whose
    /// ids contain a <c>#</c> would otherwise lose the tail silently, which is
    /// the truncation failure this repository keeps finding one field at a time.
    /// </para>
    /// <para>
    /// <b>Refused HERE when the separator is missing</b>, because the parser is
    /// the only thing that knows the token was meant to be two things. The
    /// contract sees a provider and no id and says so correctly, but it cannot
    /// say <i>you left out the #</i>.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether this word is somebody reaching for an option rather than saying
    /// something.
    /// </summary>
    /// <remarks>
    /// <b>A LEADING dash, and only leading.</b> "re-run the importer" and "fix
    /// the drop-down" are ordinary things to ask for, so a rule that read any
    /// dash would refuse most sentences. Nothing anybody types as an intent
    /// begins with one.
    /// </remarks>
    private static bool Option(string word) =>
        word.StartsWith('-');

    private static CliAction Ticket(
        string token, bool json, IReadOnlyList<string>? repositories = null, bool byHand = false,
        string? runner = null, bool attended = false,
        string? workKind = null, string? environment = null) =>
        SplitTicket(token) is var (provider, id) && provider is not null
            ? new CliAction.Fly(
                null, null, json, Provider: provider, Id: id, Repositories: repositories,
                ByHand: byHand, Runner: runner, Attended: attended,
                WorkKind: workKind, Environment: environment)
            : Unknown(
                $"gg fly --ticket takes <provider>#<id>, and '{token}' is not that shape. "
              + "Both halves are needed: the id alone does not say which tracker it is in.");

    private static CliAction CredentialAdd(IReadOnlyList<string> options, bool json)
    {
        string? repo = null;
        string? identity = null;
        IReadOnlyList<string> scopes = ReadOnly;

        for (var i = 0; i < options.Count; i += 2)
        {
            if (i + 1 >= options.Count)
            {
                return Unknown($"'{options[i]}' was given nothing to be.");
            }

            var value = options[i + 1];
            switch (options[i])
            {
                case "--repo":
                    repo = value;
                    break;

                case "--scopes":
                    scopes = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;

                case "--identity":
                    identity = value;
                    break;

                default:
                    return Unknown($"'{options[i]}' is not something gg credential add takes.");
            }
        }

        return repo is { Length: > 0 }
            ? new CliAction.CredentialAdd(repo, scopes, identity, json)
            : Unknown("gg credential add needs --repo <slug>: which repository this credential is for.");
    }

    /// <summary>The refusal's own sentence, without the parameter note.</summary>
    /// <remarks>
    /// <c>ArgumentException.Message</c> appends <c>(Parameter 'x')</c> - a note
    /// to whoever wrote the call, sitting at the end of a sentence a person
    /// reads. The contract's sentence names the value and says what is wrong
    /// with it, which is all somebody at a command line needs.
    /// </remarks>
    private static string Sentence(ArgumentException refused) =>
        refused.Message.Split(" (Parameter", StringSplitOptions.None)[0];

    /// <summary>
    /// Parses the options of <c>gg agent login</c>.
    /// </summary>
    /// <remarks>
    /// <b>Pairs, and an unknown option refused by name</b>, for the same
    /// reason <c>credential send</c> refuses one: the option somebody reaches
    /// for when scripting this is the one that would carry the code, and
    /// dropping it silently would put the code in shell history AND then ask
    /// for it anyway. The agent defaults to the one adapter there is, and is
    /// validated as a locator segment at the parse - refused, not tidied.
    /// </remarks>
    private static CliAction AgentLogin(
        IReadOnlyList<string> options, string? runner, bool json)
    {
        var agent = Gg.Local.ExecutorDeclaration.Claude;

        for (var i = 0; i < options.Count; i += 2)
        {
            if (i + 1 >= options.Count)
            {
                return Unknown($"'{options[i]}' was given nothing to be.");
            }

            var value = options[i + 1];
            switch (options[i])
            {
                case "--agent":
                    agent = value;
                    break;

                default:
                    return Unknown(
                        $"'{options[i]}' is not something gg agent login takes. It takes --runner "
                      + "and --agent; the code is read from you after the URL is shown, never "
                      + "from the command line.");
            }
        }

        if (runner is not { Length: > 0 })
        {
            return Unknown(
                "gg agent login needs --runner <id>: which machine's agent to log in. "
              + "`gg runners` lists them.");
        }

        try
        {
            _ = CredentialLocator.ForAgent(agent);
        }
        catch (ArgumentException refused)
        {
            return Unknown($"gg agent login --agent: {Sentence(refused)}");
        }

        return new CliAction.AgentLogin(runner, agent, json);
    }

    /// <summary>
    /// Parses the options of <c>gg credential send</c>.
    /// </summary>
    /// <remarks>
    /// <b>Pairs, like <c>add</c>'s, and an unknown option is refused by name
    /// rather than ignored.</b> That matters more here than anywhere else in
    /// this parser: the option somebody reaches for when they want to script
    /// this is one that would carry the value itself, and silently dropping it
    /// would put a token in shell history AND then ask for one anyway.
    /// <para>
    /// It is not spelled here, and that is not squeamishness -
    /// <c>CredentialArgsTests</c> reads every flag literal out of this file's
    /// source, comments included, and refuses any that names secret material.
    /// A comment naming one is worth catching too: it is how somebody
    /// considering the flag leaves a trace before adding it.
    /// </para>
    /// </remarks>
    private static CliAction CredentialSend(
        IReadOnlyList<string> options, string? runner, bool json)
    {
        string? repo = null;
        string? agent = null;

        for (var i = 0; i < options.Count; i += 2)
        {
            if (i + 1 >= options.Count)
            {
                return Unknown($"'{options[i]}' was given nothing to be.");
            }

            var value = options[i + 1];
            switch (options[i])
            {
                case "--runner":
                    runner = value;
                    break;

                case "--repo":
                    repo = value;
                    break;

                case "--agent":
                    agent = value;
                    break;

                default:
                    return Unknown(
                        $"'{options[i]}' is not something gg credential send takes. It takes "
                      + "--runner and one of --repo or --agent; the secret is never an "
                      + "argument, because an argument is in shell history and in ps output "
                      + "before gg has run.");
            }
        }

        if (runner is not { Length: > 0 })
        {
            return Unknown(
                "gg credential send needs --runner <id>: which machine to put it on. "
              + "`gg runners` lists them.");
        }

        // ONE SEND, ONE CREDENTIAL. Two locators would be two files and one
        // secret, and the refusal says which flag to drop.
        if (repo is { Length: > 0 } && agent is { Length: > 0 })
        {
            return Unknown(
                "gg credential send takes --repo or --agent, not both: one send puts one "
              + "credential under one locator.");
        }

        if (agent is { Length: > 0 })
        {
            // THE CONTRACT'S RULE, BEFORE ANY NETWORK. An agent's name is a
            // key, and ForAgent refuses rather than tidies; refusing here means
            // the person is told before an introduction is spent on a send that
            // could not derive a locator.
            try
            {
                _ = CredentialLocator.ForAgent(agent);
            }
            catch (ArgumentException refused)
            {
                return Unknown($"gg credential send --agent: {Sentence(refused)}");
            }

            return new CliAction.AgentCredentialSend(runner, agent, json);
        }

        return repo is { Length: > 0 }
            ? new CliAction.CredentialSend(runner, repo, json)
            : Unknown(
                "gg credential send needs --repo <slug> or --agent <name>: which credential "
              + "to send. A slug is the one `gg credential add` registered; an agent's name "
              + "is the one GG_EXECUTOR_BINARY declares, such as claude.");
    }

    /// <summary>
    /// Parses the options of <c>gg airspace repositories add</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each of the four is refused by its own name.</b> The door refuses a
    /// blank one too, with a sentence naming it - but only after a round trip,
    /// and a person told "a registry entry is incomplete" is left to work out
    /// which quarter of it is. Saying it here costs nothing and reads the same.
    /// </para>
    /// <para>
    /// <b>The refusal for a missing provider says what a provider is FOR</b>,
    /// because somebody who left it out did so believing it could be read off
    /// the path. It cannot: it is a key a runner resolves to a host of its own,
    /// and inferring it would decide where a customer's credential goes.
    /// </para>
    /// </remarks>
    private static CliAction RepositoryRegister(IReadOnlyList<string> options, bool json)
    {
        string? name = null;
        string? provider = null;
        string? id = null;
        string? path = null;
        string? credential = null;
        string? reference = null;
        string? narrowings = null;

        for (var i = 0; i < options.Count; i += 2)
        {
            if (i + 1 >= options.Count)
            {
                return Unknown($"'{options[i]}' was given nothing to be.");
            }

            var value = options[i + 1];
            switch (options[i])
            {
                case "--name":
                    name = value;
                    break;

                case "--provider":
                    provider = value;
                    break;

                case "--id":
                    id = value;
                    break;

                case "--path":
                    path = value;
                    break;

                case "--credential":
                    credential = value;
                    break;

                case "--ref":
                    reference = value;
                    break;

                case "--narrowings":
                    narrowings = value;
                    break;

                default:
                    // NEVER IGNORED. The option somebody reaches for here is
                    // --url, and dropping it silently would register the
                    // repository with whatever else was typed and leave them
                    // sure they had said which host it is on.
                    return Unknown(
                        $"'{options[i]}' is not something gg airspace repositories add takes. "
                      + "It takes --name, --provider, --id and --path, and optionally --ref, "
                      + "--credential and --narrowings.");
            }
        }

        foreach (var (flag, given, says) in ((string, string?, string)[])
        [
            ("--name", name, "what envelopes and flights call it"),
            ("--provider", provider,
                "the key a runner resolves to a host of its own. It is not read off the "
              + "path, because which host a credential goes to is the registrar's to say"),
            ("--id", id, "the forge's own identifier, which flight identity resolves through"),
            ("--path", path, "the display path an intent is matched against"),
        ])
        {
            if (given is not { Length: > 0 })
            {
                return Unknown(
                    $"gg airspace repositories add needs {flag}: {says}. A registry entry is "
                  + "four facts and none of them can be derived from another.");
            }
        }

        return new CliAction.RepositoryRegister(
            name!, provider!, id!, path!, credential, reference, narrowings, json);
    }

    /// <summary>A refusal that says what was wrong AND what is available.</summary>
    /// <remarks>
    /// Naming what was typed matters: "unknown command" alone makes a typo in a
    /// script something you find by bisecting.
    /// </remarks>
    /// <summary>
    /// A floor from percentages, or a refusal naming what went wrong.
    /// </summary>
    /// <remarks>
    /// <b><c>--clear</c> is its own spelling rather than "no options".</b>
    /// Somebody who typed the command and forgot the share meant to set one,
    /// and silently clearing their floor is the worst available answer.
    /// </remarks>
    private static CliAction Floor(string allowance, string[] rest, bool json)
    {
        int? session = null;
        int? week = null;
        var clear = false;

        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--clear":
                    clear = true;
                    break;

                case "--session" when i + 1 < rest.Length:
                    if (!int.TryParse(rest[++i], out var s))
                    {
                        return Unknown($"'--session' takes a whole percentage, not '{rest[i]}'.");
                    }

                    session = s;
                    break;

                case "--week" when i + 1 < rest.Length:
                    if (!int.TryParse(rest[++i], out var w))
                    {
                        return Unknown($"'--week' takes a whole percentage, not '{rest[i]}'.");
                    }

                    week = w;
                    break;

                case "--json":
                    json = true;
                    break;

                default:
                    return Unknown($"'{rest[i]}' is not something `gg allowances floor` takes.");
            }
        }

        if (clear && (session is not null || week is not null))
        {
            return Unknown(
                "'--clear' and a share are opposite instructions. Pass one or the other.");
        }

        if (!clear && session is null && week is null)
        {
            return Unknown(
                "Say what to keep back - '--session 33', '--week 25', or both - or '--clear' "
              + "to keep nothing. An empty floor and a cleared one are the same state, and "
              + "guessing which you meant would clear a reserve you were setting.");
        }

        return new CliAction.AllowanceFloor(allowance, session, week, json);
    }

    private static CliAction Override(string allowance, string[] rest, bool json)
    {
        var minutes = 0;
        var reason = "";

        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--minutes" when i + 1 < rest.Length:
                    if (!int.TryParse(rest[++i], out minutes))
                    {
                        return Unknown($"'--minutes' takes a whole number, not '{rest[i]}'.");
                    }

                    break;

                case "--reason" when i + 1 < rest.Length:
                    reason = rest[++i];
                    break;

                case "--json":
                    json = true;
                    break;

                default:
                    return Unknown($"'{rest[i]}' is not something `gg allowances override` takes.");
            }
        }

        return minutes < 1 || string.IsNullOrWhiteSpace(reason)
            ? Unknown(
                "An override needs '--minutes' and '--reason'. It spends somebody else's "
              + "allowance, and they read the reason - so there is no default for either.")
            : new CliAction.AllowanceOverride(allowance, minutes, reason, json);
    }

    private static CliAction.Unknown Unknown(string problem) =>
        new(problem + Environment.NewLine + Environment.NewLine
          + "usage:" + Environment.NewLine
          + string.Join(Environment.NewLine, Verbs.Select(v => "  " + v)));
}
