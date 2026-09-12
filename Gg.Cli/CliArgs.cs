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
    public sealed record RunnerTools : CliAction;

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
    public sealed record RunnerRead(string Provider, string Host, string? Credential) : CliAction;

    /// <summary>The resident runner: pull decided pool actions, act, attest.</summary>
    public sealed record RunnerMaintain(string Pool) : CliAction;

    /// <summary>
    /// Opens a flight. Exactly one payload: <see cref="Text"/>, <see cref="Uri"/>,
    /// or <see cref="Provider"/> and <see cref="Id"/> together.
    /// </summary>
    public sealed record Fly(
        string? Text, string? Uri, bool Json, string? Provider = null, string? Id = null,
        string? Repository = null,
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

    public sealed record Runners(bool Json) : CliAction, IEmitsResult;

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
        string Role, string Name, string Parent, bool Json) : CliAction, IEmitsResult;

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
        "  --runner <id>                open it for one machine",
        "  --attended                   and watch it from wherever you are",
        "gg flights [--all] [--intent <provider>#<id>|<uri>]  flights in the air, or every one",
        "gg show <flight>               one flight, by GG-42 or by id",
        "gg log <flight>                a flight's log",
        "gg runners                     the runners this tenant has",
        "gg plan [flight]               what must hold before a flight can start",
        "gg gates                       flights stopped, waiting on somebody",
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
        "gg runner watch <id>           watch what its flight is saying, as it says it",
        "gg runner repin <id>           trust a runner's key again after it changed",
        "gg invite                      a link that makes somebody a second principal here",
        "gg credential add --repo <slug>  register a credential (the value is prompted for)",
        "gg credential list             the references the control plane holds",
        "gg credential rm <id>          forget one, here and there",
        // WAS "the repositories this tenant has registered", which describes
        // neither `show` (the topology - envelope names and their roles) nor
        // the working-copy verbs beside it. Repositories are a different read
        // and the console is its only caller.
        "gg airspace show|pull|diff|apply  its names, and its working copy",
        "    apply --declare-names        declare names it needs, under root, first",
        "gg airspace retire <name>      retire a name - always opens a gate",
        "gg airspace name <role> <name> [--under <parent>]  declare a name a document can reach",
        "gg envelope show               the rules governing this tenant's flights",
        "gg strategy apply <name> <file>  manage a pool under the named strategy",
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

        var rest = Without(
            args.Where(a => a != "--json" && a != "--all" && a != "--hand"
                         && a != "--attended" && a != "--declare-names"),
            "--runner");

        // AND REFUSED ON ANYTHING THAT IS NOT `fly`. Stripping it globally would
        // accept `gg flights --hand` and do nothing - a flag that reads as an
        // instruction and is not one, which is exactly what IEmitsResult stops
        // `--json` being. Done here rather than by a type because only one verb
        // has a hand.
        if ((attended || runner is not null) && rest is not ["fly", ..])
        {
            return Unknown(
                "--attended and --runner are flags on `gg fly`: they say which machine a "
              + "flight is for and whether somebody will be watching it. On any other verb "
              + "they would read as an instruction and do nothing.");
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
            ["runner", "read", .. var read] => ReadArguments(read),
            ["runner", "maintain", var pool] => new CliAction.RunnerMaintain(pool),
            ["runner", "labels"] => new CliAction.RunnerLabels(json),
            ["runner", "retire", var retireId] => new CliAction.RunnerRetire(retireId, json),
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
            ["runner", "watch"] => Unknown(
                "gg runner watch needs one runner id. Run gg runners to see the fleet - and "
              + "a runner answers only while it is flying something opened to be watched."),

            // `--intent <provider>#<id>` is positional rather than pulled out
            // by the pre-scan above, and the difference is that it takes a
            // VALUE: a value-taking flag stripped position-independently is
            // how the value gets mistaken for a verb.
            ["flights", "--intent", var token] => Correlate(token, json, all),
            ["flights"] => new CliAction.Flights(json, all),
            ["flights", ..] => Unknown(
                "gg flights takes --all, --json, and --intent <provider>#<id> or a uri."),
            ["runners"] => new CliAction.Runners(json),
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

            ["airspace", "name", var role, var named, "--under", var parent] =>
                new CliAction.AirspaceName(role, named, parent, json),
            ["airspace", "name", var role, var named] =>
                new CliAction.AirspaceName(role, named, Gg.Contracts.Roles.Root, json),
            ["airspace", "name", ..] => Unknown(
                "gg airspace name takes a role and a name, in that order - "
              + "gg airspace name narrowing pci. The role is one of work-kind, narrowing or "
              + "strategy, and --under names the parent when it is not root."),
            ["airspace", ..] => Unknown(
                "gg airspace takes show, pull, diff, apply, name or retire."),
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
            ["fly", "--uri", var uri, "--repo", var repo] =>
                new CliAction.Fly(null, uri, json, Repository: repo, ByHand: byHand, Runner: runner, Attended: attended),
            ["fly", "--ticket", var ticket, "--repo", var repo] => Ticket(ticket, json, repo, byHand, runner, attended),
            ["fly", var text, "--repo", var repo] when !Option(text) =>
                new CliAction.Fly(text, null, json, Repository: repo, ByHand: byHand, Runner: runner, Attended: attended),

                // A trailing `--repo` is somebody who meant to name one. Falling
                // through to the says-two-things arm below would diagnose the wrong
                // half of the line, and taking it as no repository would open work
                // against an empty tree and report success.
                ["fly", _, _, "--repo"] or ["fly", _, "--repo"] => Unknown(
                    "gg fly --repo needs the name a repository is registered under, e.g. "
                  + "--repo payments. Run gg airspace show to see them."),

            ["fly", "--uri", var uri] => new CliAction.Fly(null, uri, json, ByHand: byHand, Runner: runner, Attended: attended),
            ["fly", "--ticket", var ticket] => Ticket(ticket, json, byHand: byHand, runner: runner, attended: attended),

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

            ["fly", var text] => new CliAction.Fly(text, null, json, ByHand: byHand, Runner: runner, Attended: attended),
            ["fly"] => Unknown(
                "gg fly needs something to act on: some text, --uri <uri>, "
              + "or --ticket <provider>#<id>."),
            ["fly", ..] => Unknown(
                "gg fly takes one of some text, --uri or --ticket, and this has more than one. "
              + "An intent that says two things says nothing."),

            ["credential", "list"] => new CliAction.CredentialList(json),
            ["credential", "rm", var credentialId] => new CliAction.CredentialRemove(credentialId, json),
            ["credential", "rm", ..] => Unknown("gg credential rm needs one credential id. Run gg credential list."),
            ["credential", "add", .. var options] => CredentialAdd(options, json),
            ["credential", ..] => Unknown("gg credential takes add, list or rm."),

            ["show"] => Unknown("gg show needs a flight: gg show GG-42, or the id."),
            ["log"] => Unknown("gg log needs a flight: gg log GG-42, or the id."),

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
    private static CliAction ReadArguments(ReadOnlySpan<string> arguments)
    {
        string? provider = null;
        string? host = null;
        string? credential = null;

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
                default:
                    return Unknown(
                        $"gg runner read: '{arguments[at]}' is not one of its options. It takes "
                      + "--provider, --host and an optional --credential.");
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
            _ => new CliAction.RunnerRead(provider, host, credential),
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
        string token, bool json, string? repository = null, bool byHand = false,
        string? runner = null, bool attended = false) =>
        SplitTicket(token) is var (provider, id) && provider is not null
            ? new CliAction.Fly(
                null, null, json, Provider: provider, Id: id, Repository: repository,
                ByHand: byHand, Runner: runner, Attended: attended)
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
