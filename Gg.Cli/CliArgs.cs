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

    /// <summary>The resident runner: pull decided pool actions, act, attest.</summary>
    public sealed record RunnerMaintain(string Pool) : CliAction;

    /// <summary>
    /// Opens a flight. Exactly one payload: <see cref="Text"/>, <see cref="Uri"/>,
    /// or <see cref="Provider"/> and <see cref="Id"/> together.
    /// </summary>
    public sealed record Fly(
        string? Text, string? Uri, bool Json, string? Provider = null, string? Id = null,
        string? Repository = null)
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
    public sealed record AirspaceShow(bool Json) : CliAction, IEmitsResult;

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
    public sealed record AirspaceApply(bool Json) : CliAction, IEmitsResult;

    /// <summary>What the working copy would change, per document.</summary>
    public sealed record AirspaceDiff(bool Json) : CliAction, IEmitsResult;

    /// <summary>Every runner's advertised labels, each with its disposition.</summary>
    public sealed record RunnerLabels(bool Json) : CliAction, IEmitsResult;

    public sealed record Invite(bool Json) : CliAction, IEmitsResult;

    public sealed record Doctor(bool Json) : CliAction, IEmitsResult;

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
    public sealed record EnvelopeShow(bool Json) : CliAction, IEmitsResult;

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
        "gg take <flight> [--return <outcome> [--note <note>]]  take a flight over, and hand it back",
        "gg runner labels               what each runner advertises, with its disposition",
        "gg invite                      a link that makes somebody a second principal here",
        "gg credential add --repo <slug>  register a credential (the value is prompted for)",
        "gg credential list             the references the control plane holds",
        "gg credential rm <id>          forget one, here and there",
        "gg airspace show|pull|diff|apply  the repositories this tenant has registered",
        "gg envelope show               the rules governing this tenant's flights",
        "gg strategy apply <name> <file>  manage a pool under the named strategy",
        "gg envelope apply <file>|-     write them back",
        "gg envelope validate <file>|-  check a file without sending it anywhere",
        "gg doctor                      check what gg needs to work",
        "gg bundle                      a redacted diagnostics bundle to send us",
        // One line each rather than "login | logout | whoami". A person looking
        // for a verb greps for it, and the compact form is the one spelling
        // nobody searches with.
        "gg login                       sign in on this machine",
        "gg logout                      forget the session here",
        "gg whoami                      who this machine is signed in as",
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

    public static CliAction Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // --json is position-independent, because a person will type it in
        // both places and being told off for one of them is not helpful.
        var json = args.Contains("--json", StringComparer.Ordinal);
        // --all is position-independent for --json's reason, and stripped the
        // same way so it cannot be mistaken for a verb.
        var all = args.Contains("--all", StringComparer.Ordinal);
        var rest = args.Where(a => a != "--json" && a != "--all").ToArray();

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
            ["runner", "maintain", var pool] => new CliAction.RunnerMaintain(pool),
            ["runner", "labels"] => new CliAction.RunnerLabels(json),

            // `--intent <provider>#<id>` is positional rather than pulled out
            // by the pre-scan above, and the difference is that it takes a
            // VALUE: a value-taking flag stripped position-independently is
            // how the value gets mistaken for a verb.
            ["flights", "--intent", var token] => Correlate(token, json, all),
            ["flights"] => new CliAction.Flights(json, all),
            ["flights", ..] => Unknown(
                "gg flights takes --all, --json, and --intent <provider>#<id> or a uri."),
            ["runners"] => new CliAction.Runners(json),
            ["airspace", "show"] => new CliAction.AirspaceShow(json),
            ["airspace", "pull"] => new CliAction.AirspacePull(json),
            ["airspace", "apply"] => new CliAction.AirspaceApply(json),
            ["airspace", "diff"] => new CliAction.AirspaceDiff(json),
            ["airspace", ..] => Unknown("gg airspace takes show, pull, diff or apply."),
            ["plan"] => new CliAction.Plan(null, json),
            ["plan", var flight] => new CliAction.Plan(flight, json),
            ["invite"] => new CliAction.Invite(json),
            ["doctor"] => new CliAction.Doctor(json),
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
            ["envelope", "show"] => new CliAction.EnvelopeShow(json),
            ["envelope", "apply", var source] => new CliAction.EnvelopeApply(source, json),
            ["strategy", "apply", var name, var source] =>
                new CliAction.StrategyApply(name, source, json),
            ["envelope", "apply"] => Unknown(
                "gg envelope apply needs a file, or - to read the envelope from stdin."),
            ["envelope", "validate", var source] => new CliAction.EnvelopeValidate(source, json),
            ["envelope", "validate"] => Unknown(
                "gg envelope validate needs a file, or - to read the envelope from stdin."),
            ["envelope", ..] => Unknown("gg envelope takes show, apply or validate."),

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
                new CliAction.Fly(null, uri, json, Repository: repo),
            ["fly", "--ticket", var ticket, "--repo", var repo] => Ticket(ticket, json, repo),
            ["fly", var text, "--repo", var repo] when !Option(text) =>
                new CliAction.Fly(text, null, json, Repository: repo),

            // A trailing `--repo` is somebody who meant to name one. Falling
            // through to the says-two-things arm below would diagnose the wrong
            // half of the line, and taking it as no repository would open work
            // against an empty tree and report success.
            ["fly", _, _, "--repo"] or ["fly", _, "--repo"] => Unknown(
                "gg fly --repo needs the name a repository is registered under, e.g. "
              + "--repo payments. Run gg airspace show to see them."),

            ["fly", "--uri", var uri] => new CliAction.Fly(null, uri, json),
            ["fly", "--ticket", var ticket] => Ticket(ticket, json),

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

            ["fly", var text] => new CliAction.Fly(text, null, json),
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
    private static (string? Provider, string? Id) SplitTicket(string token)
    {
        var separator = token.IndexOf('#', StringComparison.Ordinal);

        return separator <= 0 || separator == token.Length - 1
            ? (null, null)
            : (token[..separator], token[(separator + 1)..]);
    }

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

    private static CliAction Ticket(string token, bool json, string? repository = null) =>
        SplitTicket(token) is var (provider, id) && provider is not null
            ? new CliAction.Fly(null, null, json, Provider: provider, Id: id, Repository: repository)
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
    private static CliAction.Unknown Unknown(string problem) =>
        new(problem + Environment.NewLine + Environment.NewLine
          + "usage:" + Environment.NewLine
          + string.Join(Environment.NewLine, Verbs.Select(v => "  " + v)));
}
