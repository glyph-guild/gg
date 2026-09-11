using System.Diagnostics;
using System.Reflection;
using Gg.Cli;
using Gg.Client;
using Gg.Console;
using Gg.Contracts;
using Gg.Local;

return CliArgs.Parse(args) switch
{
    CliAction.LaunchConsole => await LaunchConsoleAsync(),
    // Taking a flight over runs in the DEVELOPER role like every other verb here,
    // and deliberately not in the console: a headless machine has no terminal, and
    // that is the whole point of it being a verb.
    CliAction.Take take => await TakeAsync(take.Json, commands => take.Return is { } outcome
        ? commands.ReturnAsync(take.Reference, outcome, take.Note)
        : commands.TakeAsync(take.Reference)),
    CliAction.PrintVersion => PrintVersion(),
    CliAction.Login => await AuthAsync(commands => commands.LoginAsync(Environment.MachineName)),
    CliAction.Logout => await AuthAsync(commands => commands.LogoutAsync()),
    CliAction.WhoAmI => await AuthAsync(commands => commands.WhoAmIAsync()),
    // BEFORE ANYTHING THAT PRINTS, and that placement is the whole contract.
    // Stdout IS the protocol here: one line of narration and the agent sees a
    // server that never initialized rather than a tool that failed.
    // THE ENVIRONMENT IS READ HERE AND NOWHERE DEEPER. The server is a function
    // of what it is handed - that is what makes it safe to run as a child of a
    // process the threat model treats as compromised - so where a composing
    // session's intent goes is an argument to it rather than something it goes
    // and finds. Null on every fleet launch, which is almost all of them, and
    // the tool says so out loud rather than failing quietly.
    // BY NAME, BOTH OF THEM, for EveryPortIsPassedTests' reason one project
    // over: an optional parameter is one the compiler never asks about, so a
    // value that is not passed here is a feature that is silently off rather
    // than a build that fails. documentRoot was the fourth argument and was
    // never supplied - submit_document refused every call it was ever given,
    // for as long as it has existed, while its own tests passed a root
    // directly and stayed green.
    CliAction.RunnerTools => await PlatformToolServer.RunAsync(
        System.Console.In,
        System.Console.Out,
        intentPath: Environment.GetEnvironmentVariable(IntentTool.PathVariable),
        documentRoot: Environment.GetEnvironmentVariable(DocumentTool.RootVariable),
        // THE REAL CHILD, named here and nowhere else. The tool refuses when
        // it is not given one rather than reaching for a default, so this line
        // is the whole difference between a pull tool that works and one that
        // explains it was never wired.
        pull: AirspacePullChild.Run,
        inForce: Environment.GetEnvironmentVariable(AirspaceContextTool.EnvelopeVariable)),
    // THE SAME CONTRACT, one server over. Stdout is the protocol here too, so
    // nothing on this path may print - including the credential resolution,
    // which fails as a tool error the agent can read rather than as a line.
    CliAction.RunnerRead read => await RunnerReadAsync(read),
    CliAction.RunnerUp or CliAction.RunnerServe => await RunnerUpAsync(),
    CliAction.RunnerMaintain maintain => await RunnerMaintainAsync(maintain.Pool),

    // BEFORE THE ORDINARY ARM, because a pattern that matched both would take
    // whichever came first - and it was the ordinary one, which is how
    // `--hand` parsed for a whole slice and did nothing.
    CliAction.Fly { ByHand: true } hand => await HandAsync(hand),
    CliAction.Fly fly => await EmitAsync(
        fly.Json, c => c.FlyAsync(
            fly.Text, fly.Uri, provider: fly.Provider, id: fly.Id, repository: fly.Repository,
            runner: fly.Runner, attended: fly.Attended)),
    CliAction.Flights flights => await EmitAsync(
        flights.Json, c => c.ListAsync(flights.All, intent: flights.Intent)),
    CliAction.Show show => await EmitAsync(show.Json, c => c.ShowAsync(show.Reference)),
    CliAction.Log log => await EmitAsync(log.Json, c => c.LogAsync(log.Reference)),
    CliAction.Runners runners => await EmitAsync(runners.Json, c => c.RunnersAsync()),
    CliAction.Plan plan => await EmitAsync(plan.Json, c => c.PlanAsync(plan.Flight)),
    CliAction.AirspaceShow airspace => await EmitAsync(airspace.Json, c => c.AirspaceAsync()),
    // THE WORKING COPY IS WHERE YOU ARE. Nothing configurable, because a flag
    // naming the tree would be a second place the estate's location is written
    // down - and the ADR is explicit that the repository is just a repository.
    CliAction.AirspacePull pull => await EmitAsync(
        pull.Json, c => c.AirspacePullAsync(EstateRoot())),
    CliAction.AirspaceDiff diff => await EmitAsync(
        diff.Json, c => c.AirspaceDiffAsync(EstateRoot())),
    CliAction.AirspaceApply apply => await EmitAsync(
        apply.Json, c => c.AirspaceApplyAsync(EstateRoot(), apply.DeclareNames)),
    CliAction.AirspaceRetire retiring => await EmitAsync(
        retiring.Json, c => c.RetireNameAsync(retiring.Name)),
    CliAction.AirspaceName declaring => await EmitAsync(
        declaring.Json,
        c => c.DeclareNameAsync(declaring.Role, declaring.Name, declaring.Parent)),
    CliAction.RunnerLabels labels => await EmitAsync(labels.Json, c => c.RunnerLabelsAsync()),
    CliAction.RunnerRepin repin =>
        await EmitAsync(repin.Json, c => c.RepinRunnerAsync(repin.RunnerId)),
    CliAction.RunnerRetire retire =>
        await EmitAsync(retire.Json, c => c.RetireRunnerAsync(retire.RunnerId)),
    CliAction.RunnerWatch watch => await WatchAsync(watch),
    CliAction.Invite invite => await EmitAsync(invite.Json, c => c.InviteAsync()),
    CliAction.Why why => await EmitAsync(why.Json, c => c.WhyAsync(why.Flight, why.Obligation)),
    CliAction.Gates gates => await EmitAsync(gates.Json, c => c.GatesAsync()),
    CliAction.Decide decide => await EmitAsync(decide.Json, c => c.DecideAsync(
        decide.Flight, decide.Obligation, decide.Outcome, Observed(decide.Json), decide.Reason)),
    // STOPPING A FLIGHT THAT COULD STILL HAVE BEEN DONE. Not withdrawing: the
    // question is still real and a person is stopping the attempt.
    CliAction.Ground ground => await EmitAsync(
        ground.Json, c => c.GroundAsync(ground.Reference, ground.Because)),
    // LOCAL, AND THE ONLY VERB THAT READS ANOTHER TOOL'S FILES. The
    // transcripts belong to the executor; this walks them for five values and
    // keeps nothing else, which is the boundary AllowanceLedger states and
    // holds.
    CliAction.Allowance allowance => EmitLocal(allowance.Json, AllowanceNow),

    // AND THE FLEET'S, WHICH IS A ROUND TRIP. The verb above contacts nothing;
    // this one needs a session, because what every other machine reported is
    // not a fact this disk holds.
    CliAction.Allowances fleet => await EmitAsync(fleet.Json, c => c.AllowancesAsync()),

    // A SHARE AS A PERCENTAGE ON THE WAY IN. The contract carries a fraction
    // because a fraction has one spelling; a person says "keep a third".
    CliAction.AllowanceFloor floor => await EmitAsync(floor.Json, c => c.FloorAsync(
        floor.Name,
        floor.SessionPercent is { } session ? session / 100.0 : null,
        floor.WeekPercent is { } week ? week / 100.0 : null)),

    CliAction.AllowanceOverride spend => await EmitAsync(spend.Json, c => c.OverrideFloorAsync(
        spend.Name, spend.Minutes, spend.Reason)),

    CliAction.Doctor doctor => await DoctorAsync(doctor.Json),
    CliAction.Update update => await UpdateReportAsync(update.Json),
    CliAction.Bundle bundle => await BundleAsync(bundle.Json),

    CliAction.EnvelopeShow show => await EnvelopeAsync(show.Json, c => c.ShowAsync()),
    CliAction.EnvelopeApply apply =>
        await EnvelopeAsync(apply.Json, c => c.ApplyAsync(ReadEnvelope(apply.Source))),
    CliAction.StrategyApply strategy =>
        await StrategyAsync(strategy.Json,
            c => c.ApplyAsync(strategy.Name, ReadEnvelope(strategy.Source))),
    // No client and no session: validate contacts nothing, so a syntax error
    // costs no round trip and works with no network at all.
    CliAction.EnvelopeValidate check => EmitLocal(check.Json, () =>
        EnvelopeCommands.Validate(ReadEnvelope(check.Source))),

    // THE FIRST FOUR CONFIG VERBS CONTACT NOTHING. What this machine is
    // configured to do is a fact about this machine, so a person on a plane can
    // still read it, check it and change it.
    CliAction.ConfigShow show => EmitLocal(show.Json, () =>
        ConfigCommands.Show(
            ConsoleEnvironment.Read(InForce.Configuration),
            path: null,
            file: InForce.Configuration)),

    CliAction.ConfigValidate check => EmitLocal(check.Json, () =>
        ConfigCommands.Validate(ReadEnvelope(check.Source))),

    // SEEDED FROM WHAT IS IN FORCE, so writing the file changes no resolved
    // value - it records what this machine was already doing.
    CliAction.ConfigInit start => EmitLocal(start.Json, () =>
        ConfigCommands.Init(Gg.Local.Settings.Seed())),

    CliAction.ConfigSet set => EmitLocal(set.Json, () =>
        ConfigCommands.Set(path: null, set.Key, set.Value)),

    // AND THESE TWO DO CONTACT SOMETHING, which is why they are not beside the
    // four above. An offer is a fact about somebody else's control plane, so it
    // takes a session and a network - and wired through EmitLocal they would
    // build no client, read no session, and report that nothing is offered.
    CliAction.ConfigOffered offered => await OfferAsync(
        offered.Json, o => ConfigCommands.Offered(o, InForce.Configuration)),

    CliAction.ConfigAccept accept => await OfferAsync(
        accept.Json, o => ConfigCommands.Accept(o, accept.Version)),

    CliAction.CredentialAdd add =>
        await CredentialAsync(add.Json, c => c.AddAsync(add.Repo, add.Scopes, add.Identity)),
    CliAction.CredentialList list => await CredentialAsync(list.Json, c => c.ListCredentialsAsync()),
    CliAction.CredentialRemove remove =>
        await CredentialAsync(remove.Json, c => c.RemoveCredentialAsync(remove.CredentialId)),

    CliAction.Unknown unknown => Fail(unknown.Message),
    _ => Fail("unhandled action"),
};

/// <summary>
/// What this console reads off the machine it is running on, and from the
/// offer waiting for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One function because there are two callers, and there used to be one.</b>
/// The boot applied these as a <c>with</c> block and the refresh applied
/// nothing, so each of them was preserved from the state before it - which is
/// invisible for the machine's name, harmless for the settings, and wrong for
/// the two that change: a runner registered since boot, and an offer that has
/// been taken.
/// </para>
/// <para>
/// <b>Every one is a read this process may make between sessions.</b> Three
/// are local - the environment, a file this machine wrote, its own name - and
/// the fourth asks the control plane, which is where every other read the
/// console makes already goes.
/// </para>
/// </remarks>
static AppState LocalFacts(AppState state, ControlPlaneClient client, FileSessionStore sessions)
{
    // THE FILE AGAIN, BEFORE ANYTHING BELOW READS IT. This function is the one
    // place the model learns what is on this machine, and two of the facts
    // below - the settings and the airspace path - come out of a cache that is
    // right for a verb and wrong for a console: gg is the process that writes
    // this file, so after the airspace tab set a path, the fold that was meant
    // to show it folded the copy taken at boot instead. Forgotten first rather
    // than last, because afterwards would come good on the reload after the one
    // that mattered.
    //
    // ONE READ PER BOOT OR RELOAD, which is what the cache was for: the dozen
    // readers between them still agree with each other, and the reload it sits
    // in has already made a network round trip.
    InForce.Forget();

    return state with
    {
        Settings = ConsoleEnvironment.Read(InForce.Configuration),

        // WHICH RUNNER IN THE FLEET IS THIS MACHINE'S. The id, and only the
        // id: StoredRunner beside it holds a runner token, and this model is
        // serialized under GG_STATE_DUMP and handed to the diagnostics
        // bundle. Read here for the reason the principal is - it is a file
        // this machine already wrote, not a verb.
        // THE NAMED SLOT, which is the one `gg runner up` writes. The
        // unnamed one is `gg runner maintain`'s and keeps its name so an
        // upgrade does not take a pool host down - reading it here said a
        // pool host's maintain runner was the one you are sitting at, and
        // on a laptop said there was no runner at all while one was up.
        LocalRunnerId = new FileRunnerStore(
                FileRunnerStore.PathFor(Environment.MachineName)).Read()?.RunnerId,

        // THE SAME NAME A RUNNER REGISTERS AS ITS LABEL, which is what
        // makes it the join between the fleet and the person reading it.
        // One place reads what this machine is called; Rows is pure and
        // must not, or the fleet's order would depend on the host a test
        // runs on.
        Machine = Environment.MachineName,

        // WHETHER THIS MACHINE'S OWN FILE ASKED FOR THE FLEET PANE. Here for
        // the reason the machine name is: a file this machine already has, and
        // nothing about it is the control plane's to answer - deliberately, in
        // this one's case. It carries no variable and no offerable key, so
        // opening the file is the only way to turn it on.
        //
        // AND IT IS NOT A PERMISSION. Whether the pane could hold anything is
        // AppState.IsAdmin, which comes off whoami - so a person who edits
        // this to true and is not an administrator gets a pane showing their
        // own allowances, because that is what the control plane answered.
        FleetAllowancesShown = InForce.Configuration?.FleetAllowances is true,

        // AND WHERE THIS PROCESS WAS LAUNCHED FROM, offered while the
        // airspace field is being edited: the directory somebody is
        // standing in is usually the one they mean. Read here rather than
        // by the screen, for the reason the machine name above it is.
        Cwd = Directory.GetCurrentDirectory(),

        // WHERE THIS MACHINE'S AIRSPACE IS, folded in here for the reason the
        // machine name above it is: it is a file this machine already has, and
        // nothing about it is the control plane's to answer. It sat behind the
        // estate read instead, so the tab could not say where the airspace was
        // - nor name the key that sets one - until a session, a network and a
        // tenant's applied envelope had all come good. The reads fill Names and
        // Working on top of this; the path is true before any of them.
        Estate = (state.Estate ?? new Gg.Console.EstateOnThisMachine
        {
            Uncommitted = [],
            Names = null,
        }) with
        {
            Root = Airspace(),
            IsRepository = Airspace() is { } tree && Gg.Client.Git.IsRepository(tree),
        },

        // WHAT THE CONTROL PLANE OFFERS THIS MACHINE, read here for the reason
        // the settings above it are: one place asks, and the console renders
        // what it is given.
        //
        // BETWEEN SESSIONS, WHICH IS WHERE EVERY OTHER READ IS. A UI session
        // may read a local file and nothing else - this is boot, before one
        // exists, on the same path the refresh uses afterwards. I had recorded
        // the opposite in ProjectionParityTests and reasoned from it twice.
        Offered = OfferedHere(client, sessions),
    };
}

/// <summary>
/// Takes the offer the console showed, and says what happened in one line.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fetched again rather than carried.</b> The model holds a summary, not
/// the document — deliberately, because the values a control plane proposed
/// would otherwise be dumped and bundled. So this asks once more and hands the
/// answer to the same verb the command line runs, which refuses anything but
/// the version it was told.
/// </para>
/// <para>
/// <b>Every refusal is a sentence, because there is no exit code here.</b> A
/// console has a status line and no shell: an exception reaching the loop
/// would tear the screen down over a control plane being briefly unreachable.
/// </para>
/// </remarks>
static string TakeOffered(ControlPlaneClient client, FileSessionStore sessions, string version)
{
    if (sessions.Read()?.SessionToken is not { Length: > 0 } token)
    {
        return "Not signed in, so nothing was taken.";
    }

    try
    {
        var offered = client.OfferedConfigurationAsync(token).GetAwaiter().GetResult();
        var taken = ConfigCommands.Accept(offered, version);

        return VerbOutput.ToText(taken).Trim();
    }
    catch (Gg.Client.ConfigurationRefused refused)
    {
        return refused.Message;
    }
    catch (Exception unreachable) when (unreachable is ProtocolTooOldException
                                            or ControlPlaneTooOldException
                                            or NotSignedInException
                                            or HttpRequestException
                                            or IOException)
    {
        return "Nothing was taken: " + unreachable.Message;
    }
}

/// <summary>
/// What this tenant's control plane offers this machine, or null.
/// </summary>
/// <remarks>
/// <para>
/// <b>A summary, never the document.</b> What comes back holds the values a
/// control plane proposed, and this model is serialized under
/// <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle - so the page gets
/// the version, the count, and the two facts that decide what it should say.
/// </para>
/// <para>
/// <b>Nothing here may stop a console opening.</b> Not signed in is the
/// ordinary first run; a control plane too old has not deployed the route yet;
/// unreachable is a deploy in progress. A console that refused to draw for any
/// of those would be unusable exactly when somebody opened it to find out what
/// was wrong.
/// </para>
/// </remarks>
static Gg.Console.OfferedOnThisMachine? OfferedHere(
    ControlPlaneClient client, FileSessionStore sessions)
{
    if (sessions.Read()?.SessionToken is not { Length: > 0 } token)
    {
        return null;
    }

    try
    {
        if (client.OfferedConfigurationAsync(token).GetAwaiter().GetResult() is not { } offered)
        {
            return null;
        }

        return new Gg.Console.OfferedOnThisMachine
        {
            Version = offered.Version,
            Settings = offered.Settings.Count,
            NeedsAPerson = Gg.Contracts.OfferedConfiguration.NeedsAPerson(offered),
            AlreadyAccepted = string.Equals(
                InForce.Configuration?.AcceptedOffer, offered.Version, StringComparison.Ordinal),
        };
    }
    catch (Exception refused) when (refused is NotSignedInException
                                        or ProtocolTooOldException
                                        or ControlPlaneTooOldException
                                        or HttpRequestException)
    {
        return null;
    }
}

/// <summary>
/// The envelope text, from a file or from stdin.
/// </summary>
/// <remarks>
/// "-" reads stdin, which is what makes this compose with an editor and with
/// the sync a customer keeping envelopes in git will want. Their review
/// process, our authority.
/// </remarks>
/// <summary>
/// Where the estate's working copy is: the setting, or the current directory.
/// </summary>
/// <remarks>
/// <para>
/// <b>One resolution for all three verbs.</b> Each used to call
/// <c>Directory.GetCurrentDirectory()</c> at its own call site, so a change to
/// two of them would have left each verb working while they disagreed about
/// which tree they were talking about.
/// </para>
/// <para>
/// <b>The current directory stays as a FALLBACK rather than becoming a
/// default.</b> Nothing that works today stops working - a verb typed inside
/// the tree still finds it - and a person who sets the path gets the same
/// answer wherever they run from, which is the whole reason the console can
/// have an estate at all.
/// </para>
/// </remarks>
/// <summary>
/// Where this machine's airspace is, or null when nobody has said.
/// </summary>
/// <remarks>
/// <b>NOT <see cref="EstateRoot"/>, and the difference is a defect this
/// closes.</b> That one falls back to the process's current directory,
/// which is right for a verb somebody typed in a tree they chose and wrong
/// for a console launched from wherever they happened to be: `p` wrote an
/// airspace/ tree into the launch directory, and because that is usually
/// not a git tree the dirty-tree refusal could not fire, so pull simply
/// wrote. An unset airspace has to read as unset - and `w` is how a person
/// answers it without leaving.
/// </remarks>
static string? Airspace() =>
    Settings.Value("GG_AIRSPACE", InForce.Configuration) is { Length: > 0 } named
        ? named
        : null;

/// <summary>What to say when there is no airspace to act on.</summary>
/// <remarks>
/// <b>It names the key, because the console is where this is answered now.</b>
/// A refusal that only reported the state would be the dead end the current-
/// directory fallback was papering over - and the fallback is what wrote a tree
/// into somebody's launch directory.
/// </remarks>
static string NoAirspace(string act) =>
    $"No airspace is configured, so there is nothing to {act}. Press w to say where it is.";

static string EstateRoot() =>
    Settings.Value("GG_AIRSPACE", InForce.Configuration) is { Length: > 0 } named
        ? named
        : Directory.GetCurrentDirectory();

static string ReadEnvelope(string source) =>
    source == "-" ? Console.In.ReadToEnd() : File.ReadAllText(source);

/// <summary>
/// A verb that needs nothing but the text it was handed.
/// </summary>
/// <remarks>
/// <b>An invalid envelope exits non-zero</b>, while still printing the whole
/// structured result. A validator that reports success on a document it just
/// refused is one nobody can put in a pipeline, and the pipeline is where this
/// verb earns its keep - a customer keeping envelopes in git wants their
/// review process and our authority.
/// </remarks>
static int EmitLocal(bool json, Func<VerbResult> run)
{
    try
    {
        var result = run();
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

        // A VALIDATOR THAT REPORTS SUCCESS ON A DOCUMENT IT JUST REFUSED is one
        // nobody can put in a pipeline. Both document kinds, because the second
        // was added and the exit code was not - which the tests missed, since
        // they asserted the RESULT and this harness is what turns one into an
        // exit code.
        return result is VerbResult.EnvelopeValidated { Value.Valid: false }
                      or VerbResult.ConfigValidated { Value.Valid: false }
            ? 1
            : 0;
    }
    catch (Gg.Client.ConfigurationRefused refused)
    {
        return Fail(refused.Message);
    }
    catch (IOException unreadable)
    {
        return Fail(unreadable.Message);
    }
    catch (UnauthorizedAccessException unreadable)
    {
        return Fail(unreadable.Message);
    }
}

/// <summary>
/// The two config verbs that read an offer, and what they refuse.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fetch is here and the decision is not.</b> This resolves the session,
/// asks the control plane once, and hands the answer to a static command that
/// contacts nothing - so what a machine does with an offer is testable with
/// nothing running, and this harness is the only place that has to be right
/// about the network.
/// </para>
/// <para>
/// <b>A refused offer exits non-zero.</b> It is a produced answer rather than a
/// throw - a control plane offering something this gg will not take is exactly
/// what a person needs told - and `gg config validate` has already been the
/// verb that reported a refusal and exited 0 here, because the tests asserted
/// the result and this layer decides the code.
/// </para>
/// </remarks>
static async Task<int> OfferAsync(
    bool json, Func<Gg.Contracts.OfferedConfiguration?, VerbResult> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    try
    {
        var session = new FileSessionStore().Read()?.SessionToken
            ?? throw new NotSignedInException("Not signed in. Run gg login.");

        var offered = await new ControlPlaneClient(http).OfferedConfigurationAsync(session);
        var result = run(offered);

        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

        return ExitCodes.For(result);
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (Gg.Client.ConfigurationRefused refused)
    {
        return Fail(refused.Message);
    }
    catch (ProtocolTooOldException behind)
    {
        return Fail(behind.Message);
    }
    catch (ControlPlaneTooOldException unserved)
    {
        // THE OTHER DIRECTION, and it needs its own arm because the sentence is
        // about somebody else's deployment rather than this machine. Reported
        // rather than crashed, and never as "nothing is offered".
        return Fail(unserved.Message);
    }
    catch (HttpRequestException unreachable)
    {
        return Fail(unreachable.Message);
    }
    catch (IOException unwritable)
    {
        return Fail(unwritable.Message);
    }
}

/// <summary>The envelope verbs, which need a session and the control plane.</summary>
/// <summary>
/// What this process can observe about how a decision was made.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observations, never a conclusion.</b> There is no `attended` here on purpose:
/// connection is a transport fact and attendance is a decision record, and this process
/// cannot tell them apart. A person can pipe input; a script can allocate a terminal.
/// gg says what it saw and the control plane decides what that means.
/// </para>
/// <para>
/// <b>Nothing rendered, in this version.</b> `gg decide` takes the outcome on the
/// command line, so no evidence was shown and the honest answer to "was it read" is no.
/// That will change when the console gets a modal; the field exists now because a
/// decision recorded before it existed is unclassifiable afterwards.
/// </para>
/// </remarks>
static Gg.Contracts.DecisionObservations Observed(bool json) => new()
{
    // Both ends, because either being redirected means something other than a person at
    // a terminal is driving this.
    Interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected && !json,
    EvidenceRendered = false,
    SecondsToDecide = null,
};

static async Task<int> EnvelopeAsync(bool json, Func<EnvelopeCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new EnvelopeCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return 0;
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (DirtyWorkingCopyException refusal)
    {
        // The list is the actionable part, so it reaches the person whole
        // rather than as "the tree is dirty".
        return Fail(refusal.Message);
    }
    catch (NoEnvelopeException missing)
    {
        return Fail(missing.Message);
    }
    catch (EnvelopeUnreadableException unreadable)
    {
        return Fail(unreadable.Message);
    }
    catch (EnvelopeRefusedException refused)
    {
        return Fail(refused.Message);
    }
    catch (IOException unreadable)
    {
        return Fail(unreadable.Message);
    }
}

static async Task<int> StrategyAsync(bool json, Func<StrategyCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new StrategyCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return 0;
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (StrategyUnreadableException unreadable)
    {
        return Fail(unreadable.Message);
    }
    catch (StrategyRefusedException refused)
    {
        return Fail(refused.Message);
    }
    catch (IOException unreadable)
    {
        return Fail(unreadable.Message);
    }
}

// THROUGH THE ONE READER, so the file reaches it. The default lives in
// Settings.Defaults now rather than here: it was quoted in four places and the
// page could not tell anybody what it was.
static string ControlPlaneAddress() =>
    Settings.Value("GG_CONTROL_PLANE", InForce.Configuration)!;

/// <summary>
/// Runs a verb and prints its result - one way or the other, never both.
/// </summary>
/// <remarks>
/// The single place a result becomes characters on a screen. Every verb hands
/// back a VerbResult and nothing else, so the JSON and the human rendering are
/// two views of one document rather than two implementations that agree today.
/// The console at step 4b renders the same value through the same code.
/// </remarks>
static async Task<int> EmitAsync(bool json, Func<FlightCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new FlightCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

        // THREE OUTCOMES, THREE CODES. A script has to tell "you were told no"
        // from "we do not know yet", and one non-zero cannot carry both.
        return ExitCodes.For(result);
    }
    catch (DecisionRefusedException refused)
    {
        // AN ANSWER, AND IT USED TO BE A CRASH. Every refusal on this path left as
        // an unhandled InvalidOperationException - a stack trace and exit 134,
        // which is SIGABRT and is what gg looks like when it breaks.
        return Fail(refused.Message);
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (DirtyWorkingCopyException refusal)
    {
        // THE AIRSPACE VERBS' TWO REFUSALS, and they crashed here for as long
        // as those verbs have run through this emitter. Both were routed to a
        // function that catches seven other refusals - including one whose own
        // comment above records this exact failure, in these words - and
        // neither was added to it. EnvelopeAsync, one function away, catches
        // both.
        //
        // The list of files is the actionable part, so it reaches a person
        // whole rather than as "the tree is dirty".
        return Fail(refusal.Message);
    }
    catch (EnvelopeRefusedException refused)
    {
        // NAMES THE FILES THAT WILL NOT PARSE, for the reason apply refuses at
        // all: landing the rest would put part of a changeset somebody meant
        // as a whole into the stream. A refusal that did not say which files
        // would leave a person to find them by bisecting their own tree.
        return Fail(refused.Message);
    }
    catch (FlightReferenceException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (FlightNotFoundException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (FlightIntentException refusal)
    {
        // Article XI reaching a person: the diagnosis is the actionable part
        // and collapsing it into "bad request" would throw that away.
        return Fail(refusal.Message);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return ExitCodes.Unavailable;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return ExitCodes.Unavailable;
    }
}

/// <summary>
/// Runs a takeover verb and prints what came back.
/// </summary>
/// <remarks>
/// <b>Its own emitter for one reason: the refusal.</b> Somebody else holding the
/// flight is the ordinary case this verb exists for, not a fault - so it is caught
/// and printed as a sentence with a non-zero code, rather than reaching the
/// unhandled path as a stack trace and exit 134, which is what gg looks like when
/// it breaks.
/// </remarks>
static async Task<int> TakeAsync(bool json, Func<TakeCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new TakeCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return ExitCodes.For(result);
    }
    catch (TakeoverRefusedException refused)
    {
        // REFUSED, not unavailable. A script can tell "somebody has it" from "the
        // control plane is down", and one non-zero cannot carry both.
        return Fail(refused.Message);
    }
    catch (ArgumentException refused)
    {
        return Fail(refused.Message);
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return ExitCodes.Unavailable;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return ExitCodes.Unavailable;
    }
}

/// <summary>
/// Runs a credential verb, in the credential-broker role.
/// </summary>
/// <remarks>
/// Separate from <c>EmitAsync</c> only because the refusals are different
/// ones. The result path is the same: a VerbResult, printed one way or the
/// other, and never both.
/// </remarks>
static async Task<int> CredentialAsync(bool json, Func<CredentialCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    var commands = new CredentialCommands(
        new ControlPlaneClient(http),
        new FileSessionStore(),
        new FileCredentialStore(),
        // The only way a secret enters this process, and it is a terminal.
        new ConsoleSecretPrompt());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return 0;
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (CredentialScopeException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (CredentialRefusedException refusal)
    {
        // Article XI reaching a person: the control plane refused with a
        // diagnosis and the diagnosis is the part they can act on.
        return Fail(refusal.Message);
    }
    catch (CredentialNotFoundException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return ExitCodes.Unavailable;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return ExitCodes.Unavailable;
    }
}

/// <summary>
/// Runs doctor, which reports rather than throws.
/// </summary>
/// <remarks>
/// Separate from the verbs above because it is the one that must survive
/// everything they refuse on: an unreachable control plane is a finding here,
/// not a failure, or the command would be useless in exactly the case somebody
/// runs it.
/// </remarks>
static async Task<int> DoctorAsync(bool json)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    // WHAT THIS MACHINE IS, read here because this is where the environment
    // belongs. Gg.Client references only Gg.Contracts, so the doctor is handed
    // facts rather than going looking for variables.
    var executor = Settings.Value(
        Gg.Runner.Execution.ExecutorConfiguration.BinaryVariable, InForce.Configuration);

    // NOT EstateRoot(): the doctor reports what is CONFIGURED, and the fallback
    // to the current directory is what the verbs do rather than something the
    // machine was set up with. Reporting the fallback as configuration would
    // tell a person their estate lives wherever they last ran gg from.
    var airspace = Settings.Value("GG_AIRSPACE", InForce.Configuration);

    var role = new MachineRole
    {
        ExecutorBinary = executor,
        ExecutorPresent = executor is { Length: > 0 } && File.Exists(executor),

        // WHAT IS ALREADY HERE, looked for only when nothing is configured.
        // Probing regardless would be a PATH walk on every doctor run to
        // answer a question nobody asked.
        ExecutorOnPath = executor is { Length: > 0 }
            ? null
            : Gg.Local.OnPath.Find(
                Settings.Value("GG_TAKE_COMMAND", InForce.Configuration) ?? "claude"),
        ForgeHosts = Settings.Value(
            Gg.Runner.Vcs.VcsConfiguration.HostsVariable, InForce.Configuration),
        DestinationApis = Settings.Value(
            Gg.Runner.Vcs.DestinationConfiguration.ApisVariable, InForce.Configuration),
        PoolEndpoint = Settings.Value("GG_POOL_ENDPOINT", InForce.Configuration),

        // THE SAME RESOLUTION THE VERBS USE, so the doctor cannot report one
        // tree while pull writes to another. Asked for the repository fact only
        // when there is a path to ask about - git on a path nobody configured
        // is a subprocess spent answering a question that has no subject.
        Airspace = airspace,
        AirspaceIsRepository = airspace is { Length: > 0 } && Git.IsRepository(airspace),
    };

    var report = await new Doctor(
        new ControlPlaneClient(http), new FileSessionStore(), new FileCredentialStore(),
        new Uri(baseAddress),
        addressConfigured: Settings.Resolve("GG_CONTROL_PLANE", InForce.Configuration)
            .Source != SettingSources.Default,
        // WHAT THIS MACHINE WOULD ASK, read where every other environment
        // reading happens. Without it a console offers only its own local
        // address and watching a runner from anywhere else answers "no route
        // between them" - which reads as a firewall and is a variable.
        // THROUGH THE ONE READER, like every other configurable value the root
        // resolves. Called argumentless this falls back to the environment,
        // which is right for a runner in its own process and wrong here: this
        // process has the file, and a `stun-servers` line in it would reach
        // nothing. The fallback made the doctor answer "nobody could reach
        // this machine" for a reason it had not looked at.
        stunServers: Gg.Runner.StunConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.StunConfiguration.Variable)))
        .RunAsync(role: role);

    var result = new VerbResult.Diagnosis(report);
    Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

    return report.ExitCode;
}

/// <summary>
/// `gg bundle`. Everything doctor asked, plus what a person would otherwise
/// be asked for, minus anything a runner printed.
/// </summary>
/// <remarks>
/// The flight log is fetched only when the control plane answered. Asking for
/// it anyway would turn one clear "could not connect" into two, and the second
/// one would be the one people report.
/// </remarks>
static async Task<int> BundleAsync(bool json)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    var sessions = new FileSessionStore();
    var client = new ControlPlaneClient(http);
    var report = await new Doctor(
            client, sessions, new FileCredentialStore(), new Uri(baseAddress),
        // THROUGH THE ONE READER, like every other configurable value the root
        // resolves. Called argumentless this falls back to the environment,
        // which is right for a runner in its own process and wrong here: this
        // process has the file, and a `stun-servers` line in it would reach
        // nothing. The fallback made the doctor answer "nobody could reach
        // this machine" for a reason it had not looked at.
        stunServers: Gg.Runner.StunConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.StunConfiguration.Variable)))
        .RunAsync();

    // Observed with no tree: a bundle is taken from wherever somebody happens
    // to be standing, and the locks and the tree belong to a flight rather
    // than to this machine. The fingerprint is the same one the runner
    // records, which is what lets a bundle be matched to a flight's facts.
    var environment = Gg.Runner.Facts.EnvironmentSurvey.Observe(treePath: null, Gg.Contracts.EnvironmentProvenance.Reused);

    var result = new VerbResult.Bundle(
        Gg.Client.Bundle.Build(DateTimeOffset.UtcNow, environment, report, flightLog: null));

    Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

    // Zero. A bundle is a report, not a verdict: exiting non-zero because the
    // machine it describes has a problem would make `gg bundle` unusable in
    // the script somebody writes to collect one.
    return 0;
}

/// <summary>Where a runner started from the console writes what it says.</summary>
/// <remarks>
/// Beside the live views rather than in the repository somebody happens to be
/// standing in: a log that lands in a working tree is a log that gets committed.
/// </remarks>
/// <summary>
/// Runs a child that should return at once, and answers its exit code.
/// </summary>
/// <remarks>
/// <b>Nothing is shell-interpreted.</b> The arguments were built one at a time
/// and the text goes in on standard input, so a link cannot become a second
/// command.
/// </remarks>
static int Ran(ProcessStartInfo info, string? input)
{
    using var child = Process.Start(info);

    if (child is null)
    {
        return -1;
    }

    if (input is not null)
    {
        child.StandardInput.Write(input);
        child.StandardInput.Close();
    }

    return child.WaitForExit(ConsoleLink.Grace) ? child.ExitCode : -1;
}

static string RunnerLogPath() =>
    Path.Combine(Gg.Local.LocalPaths.StateRoot(), "runner.log");

/// <summary>Where the runner on this machine says it is running.</summary>
/// <remarks>
/// Beside the log rather than beside the credentials: this is true only while a
/// process is up, and the config directory holds things that survive a reboot.
/// </remarks>
static string RunnerPidPath() =>
    Path.Combine(Gg.Local.LocalPaths.StateRoot(), "runner.pid");

static async Task<int> LaunchConsoleAsync()
{
    // The queue is loaded through the VERBS, so what the console shows is what
    // `gg flights --json` would print. There is no other route to the data.
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var client = new ControlPlaneClient(http);
    var sessions = new FileSessionStore();
    var takes = new TakeCommands(client, sessions);

    // SIGNING IN, WHICH THIS CONSOLE MAY DO BEFORE IT CAN DO ANYTHING ELSE.
    // The same commands `gg login` uses and the same session file, so a person
    // who signs in here is signed in at the command line and the other way
    // round - two ways in, one credential, and no second notion of who you are.
    //
    // The writer is the real one: the shell runs with the terminal provably
    // free, which is the same reason the secret prompt below is allowed to be.
    var auth = new AuthCommands(
        client, sessions, new StandardConsoleWriter(), new SystemClock(),
        (span, token) => Task.Delay(span, token));

    var data = new ConsoleData(
        new FlightCommands(client, sessions),
        // The console can read the credential references and forget one. It
        // cannot add one: that needs a secret typed at a prompt, and a prompt
        // inside a Terminal.Gui modal is a keyboard path with its own
        // escape-hatch rules. Registering stays a command-line act.
        new CredentialCommands(client, sessions, new FileCredentialStore(), new ConsoleSecretPrompt()),
        takes,
        // WHAT THIS TENANT SHOULD KNOW. The notices row above the queue was
        // drawn by PaneText from the first slice and assigned by nothing, so a
        // degradation the control plane reported on every call reached nobody.
        new IdentityCommands(client, sessions),
        // THE RULES IN FORCE, readable at last. Every flight the console shows
        // names this document's version and nothing could show the document.
        new EnvelopeCommands(client, sessions));

    // WHO IS SIGNED IN, OR NOBODY, AND NOBODY IS NOT AN ERROR HERE. The verbs
    // refuse with `Not signed in. Run gg login.` when the file is absent, which
    // is right where a shell can print it - and this is the one caller that has
    // taken the shell away. Asked through them, the refusal was thrown while
    // evaluating an ARGUMENT to the loader, so it landed outside the catch that
    // turns being signed out into the modal: gg on a machine that had never
    // signed in ended with a stack trace instead of the screen built for it.
    var principal = sessions.Read()?.PrincipalDisplay ?? "";

    // THE LOCAL FACTS, APPLIED BY ONE FUNCTION SO THE REFRESH APPLIES THEM
    // TOO. This used to be a `with` block right here, and the reload below
    // called the same loader and applied none of it - so the settings, this
    // machine's runner id, its name and what the control plane offers were all
    // preserved from the state before the refresh. Taking an offer and
    // reopening help showed the offer that had just been taken.
    //
    // ConsoleLoop's own reload writes this argument down one layer in: it takes
    // the whole model and answers with it rather than naming six fields,
    // because every field it did not name reset to a default. Same reasoning,
    // one layer out.
    var initial = LocalFacts(
        ConsoleStart.LoadAsync(data, principal).GetAwaiter().GetResult(), client, sessions);

    // TAKE AND HAND, PASSED FOR THE FIRST TIME. Both were optional constructor
    // arguments that only tests ever supplied, so the console's takeover key
    // answered "this console is not configured to take flights over" for the whole
    // of slices five and six while every piece of the machinery underneath was
    // written and tested.
    //
    // TakeSession is given a CLAIM rather than only a command to spawn. A console
    // that handed over a terminal without claiming would reintroduce the failure
    // slice seven exists to remove: two people on two machines both working one
    // flight, each believing they hold it.
    //
    // HAND IS STILL NOT PASSED, and that is a stated gap rather than an oversight.
    // HandSession needs two ports the product does not have: an `infer` that spawns
    // an agent to propose what appears to have been done, and an `ask` that reads a
    // confirmation from the terminal. Building the first means invoking an executor
    // from the console, which is a boundary slice seven does not touch. So the
    // hand-back key still answers "this console is not configured to hand flights
    // back", and gg:ConsoleTakeWiringTests says so out loud rather than asserting a
    // wiring that would have to be faked to pass.
    // ONE SET OF TAILS, SHARED. The loop advances the pane between sessions and
    // the screen advances it on a timer during one; both resume from the same
    // offset or the same lines arrive twice. It is owned here, outside every UI
    // lifetime, which is what keeps "a session retains nothing" true.
    // THE ONE RUNNER THIS CONSOLE IS WATCHING, if any. It holds the
    // conversation and the buffer its output lands in; the pane never knows the
    // difference, because ILiveSource is Read-and-Exists and says nothing about
    // where the lines came from.
    using var watched = new Gg.Console.WatchedRunner(
        (runnerId, onLine, onStep, onOpen, token) =>
            new WatchARunner(
                new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(baseAddress) }),
                new ConsoleChannel(
                    Gg.Runner.StunConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.StunConfiguration.Variable, InForce.Configuration)), TimeSpan.FromSeconds(20)))
            .WatchAsync(
                new FileSessionStore().Read()?.SessionToken ?? "",
                runnerId,
                new PinnedRunnerKeys(),
                lines: 200,
                DateTimeOffset.UtcNow,
                write: onLine,
                follow: true,
                saying: onStep,
                opened: onOpen,
                cancellationToken: token)
            // THE SENTENCE, WHICH IS THE POINT OF ASKING. Only the watch knows
            // whether nobody answered because the machine is away or because
            // the flight was never opened to be watched.
            .ContinueWith(done => done.Result.Said, TaskScheduler.Default),
        () => DateTimeOffset.UtcNow);

    // THE WATCHED FLIGHT FIRST, THEN THE FILE. A runner on this machine writes
    // its live view here and a fleet runner writes it on the fleet host, so the
    // two sources answer the same question about different flights - and the
    // fallback is what keeps every flight that is NOT being watched drawing
    // exactly as it did.
    var tails = new LiveTails(flightId =>
        watched.SourceFor(flightId) ?? new LiveTail(Gg.Local.LocalPaths.LiveView(flightId)));

    // THE RUNNER THIS CONSOLE MAY START, and the log it writes. The handle on
    // the child is owned here, outside every UI lifetime, for the reason the
    // reader sessions are: a model that is written to disk may hold a pid and
    // not a process, and a session must retain nothing across a rebuild.
    var runnerLog = new RunnerLog(RunnerLogPath());
    using var runner = new RunnerAtHand(
        runnerLog, new RunnerPidFile(RunnerPidPath()), new LocalProcesses(), () =>
    {
        if (Gg.Local.SelfInvocation.Current is not { } self)
        {
            return null;
        }

        var info = new ProcessStartInfo(self.Command)
        {
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in self.Under("runner"))
        {
            info.ArgumentList.Add(argument);
        }

        info.ArgumentList.Add("up");

        var child = Process.Start(info);

        if (child is null)
        {
            return null;
        }

        // DRAINED AND FLUSHED, because a child whose pipes fill up stops and a
        // log nothing flushes is empty until the runner exits. RunnerLog owns
        // both halves, where a test can hold them.
        _ = Task.WhenAll(
            RunnerLog.CaptureAsync(
                child.StandardOutput.BaseStream, RunnerLogPath(), CancellationToken.None),
            RunnerLog.CaptureAsync(
                child.StandardError.BaseStream, RunnerLogPath(), CancellationToken.None));

        return child;
    });

    // THE TRACKERS THIS MACHINE CAN BROWSE, and this line is the whole of what
    // was missing. `ConsoleLoop` has taken a browser since the pane was built;
    // nothing ever passed one, so `ConfiguredWorkBrowser` was constructed
    // nowhere and `ReaderSessions` only in a test - and the pane answered "No
    // tracker is configured to browse" on machines whose tracker was configured
    // correctly, whose credential resolved, and whose reader answered a
    // handshake when started by hand.
    //
    // OWNED HERE, because it holds child processes. `ReaderSessions` is
    // `LiveTails`' shape for that reason: a session must retain nothing across
    // a UI rebuild, so whoever composed the console owns the handles and stops
    // them at the end.
    //
    // NO CREDENTIAL PASSES THROUGH THIS. The declaration carries a locator at
    // most, and the child resolves it for itself - which is what keeps a
    // console that browses out of the business of holding secrets.
    //
    // HOW LONG IT WAITS, and a person is watching this one - which is what sets
    // it. A tracker that has not answered in fifteen seconds is one the pane
    // should say so about rather than keep a keystroke waiting on; SpawnedReader
    // turns this into a deadline and reports the number it waited.
    await using var readers = new Gg.Console.ReaderSessions(
        Gg.Local.IntentConfiguration.FromEnvironment(
            Settings.Value(Gg.Local.IntentConfiguration.ReadersVariable, InForce.Configuration),
            Settings.Value(Gg.Local.IntentConfiguration.ServedVariable, InForce.Configuration)), TimeSpan.FromSeconds(15));

    // THE TAB IN FRONT OF SOMEBODY, EVERY THIRTY SECONDS. On a task, so the
    // session folds a finished answer rather than waiting for one - the
    // argument for that is written out in AutoRefresh, and the short of it is
    // that a keyboard frozen for as long as the control plane takes is the
    // thing the rule against reading in a session protects.
    var refresh = new AutoRefresh(
        tab => Task.Run(() => ConsoleRefresh.ForTabAsync(data, tab)),
        new SystemClock(),
        TimeSpan.FromSeconds(30));

    // BOTH ENDS OF SIGNING IN, HELD IN ONE PLACE. The session owns the device
    // code and does the polling; the screen has to end its lifetime when that
    // poll lands, because the fold and the reload belong to the loop with the
    // terminal free. Neither can reach the other, and this is the only place
    // that has both - so it is hoisted out of the argument list below.
    //
    // The two halves of `gg login` rather than the verb, because the verb
    // fetches the code and blocks on it in one breath - the code would only
    // ever appear in what was printed before Terminal.Gui painted over it.
    // The device code stays inside SignInSession; what comes back to the
    // model is what a person reads off the screen.
    var signIn = new SignInSession(
        () => auth.StartAsync(Environment.MachineName).GetAwaiter().GetResult(),
        started => auth.AwaitApprovalAsync(started).GetAwaiter().GetResult());

    var final = new ConsoleLoop(
        new TerminalGuiSession(
            tails, runnerLog, refresh, signIn.Landed,
            // A READ A KEYPRESS ASKED FOR, folded on the tick. Opening a flight
            // used to end the session for exactly one request, which is a whole
            // screen taken away and given back - AutoRefresh's argument, one
            // keypress over.
            //
            // AND IT ANSWERS THE COMMAND IT IS GIVEN, which it did not. The
            // port has carried a Command since it was written and this lambda
            // discarded it, so three commands were served one read: pressing
            // the key that fetches the airspace ran the flight-log read, which
            // short-circuits when no flight is open. The airspace tab's
            // documents were unreachable for that reason and two others.
            //
            // EACH ARM ANSWERS WITH A PATCH rather than a model, which is the
            // port's own rule: a read answering with a whole AppState is a
            // snapshot taken before the person moved and applied after.
            reads: new Gg.Console.BackgroundReads((asked, current) => Task.Run(() =>
                asked switch
                {
                    Gg.Console.Command.ToggleEnvelope =>
                        // BOTH READS, as the one key has always meant: what
                        // governs, and where to change it. Estate second so a
                        // failing topology cannot cost the envelope.
                        (Func<AppState, AppState>)(_ => Gg.Console.ConsoleEstate.Read(
                            data, Airspace(), Gg.Console.ConsoleEnvelope.Read(data, current))),

                    Gg.Console.Command.ToggleRepositories =>
                        _ => Gg.Console.ConsoleRepositories.Read(data, current),

                    // THE FLIGHT'S STORY, which is what this port was built
                    // for - and named rather than defaulted.
                    Gg.Console.Command.ShowFlight =>
                        Gg.Console.ConsoleFlightLog.Patch(data, current),

                    // AND A FOURTH THROWS RATHER THAN GUESSING, which is the
                    // rule this codebase applies wherever a value decides
                    // what happens: ConsoleLoop throws on an exit command it
                    // does not know and Changeset.Rank throws on a direction
                    // it cannot place, because unknown is not neutral. A
                    // default arm here is precisely how three commands came
                    // to be served one read - the airspace fetched a flight's
                    // story and found none.
                    _ => throw new InvalidOperationException(
                        $"'{asked}' is in ShellCommands.Reads and this reader has no arm "
                      + "for it, so a keypress would fetch somebody else's answer. Add "
                      + "one, or take the command out of Reads."),
                }))),
        // HOSTED, SO GG KEEPS A ROW WHILE THE EDITOR HAS THE SCREEN. The
        // handoff is the same one it always was - text out, a real process, text
        // back - and the difference is that gg mediates the terminal instead of
        // giving it away, which is what lets a person still see which flight
        // they are writing for.
        //
        // It decides for itself whether there is a terminal to host on and takes
        // the old unhosted path when there is not: CI, a pipe, and Windows,
        // where there is no /dev/tty to open. That decision lives inside the one
        // type on purpose - a second construction site here would be a second
        // place to answer it, and the two would eventually disagree.
        new PtyEditorSession(
            // RESOLVED HERE, because the session's own fallback reads the
            // variable and would miss the file. Gg.Console can reach
            // Settings - it is Gg.Local - but not the file this root read
            // once, so the value is handed over rather than looked up.
            Settings.Value("EDITOR", InForce.Configuration)),
        // THE OTHER WAY TO COMPOSE, and it is passed here for the same reason
        // the editor is: this is the only place that may name a self-invocation.
        // gg serves its own intent tool by starting itself again, and a console
        // that could name that would be a console that can act as a runner.
        //
        // A KEY THAT OFFERED THIS AND FELL BACK WOULD READ AS A FLICKER, which
        // is what EveryPortIsPassedTests exists to catch - it caught this one.
        compose: new PtyAgentSession(
            Settings.Value("GG_TAKE_COMMAND", InForce.Configuration),
            // THE RULES IN FORCE, FOR THE PANEL TO SHOW. Read here because this
            // is the only place that may name the control plane, and read once
            // per compose session rather than on the keypress - the panel opens
            // over a child that already has the screen, and a key that waits on
            // a network round trip is one somebody presses again.
            envelope: () => ConsoleEnvelope.Read(data, new AppState()).Envelope),
        // NAMED, like every other port. Fourteen optional arguments and one
        // positional is how a port gets passed to the wrong slot, and
        // EveryPortIsPassedTests can only see the ones that say their name.
        take: new TakeSession(
            command: Settings.Value("GG_TAKE_COMMAND", InForce.Configuration),
            claim: reference =>
            takes.ClaimAsync(reference).GetAwaiter().GetResult()),
        hand: null,
        // THE WRITE PATH. Async verbs, a synchronous shell, and the bridge at the
        // edge - the same one ConsoleStart.LoadAsync uses two lines up. Without
        // this the gate keys resolved, reached the reducer and did nothing.
        // The prompt is the one the credential verb already uses on the command
        // line. It runs here with the UI torn down and the terminal free, which is
        // what answers the old objection to registering from a console: the
        // escape-hatch rules a modal would need do not apply to a process that owns
        // the screen.
        actions: new VerbConsoleActions(data, new ConsoleSecretPrompt()),
        tails: tails,
        // THE READ PATH, AND IT IS THE SAME ONE THE BOOT TOOK. Passing the boot
        // itself is what makes a refresh mean "as if you had just opened it"
        // rather than "as much of it as somebody remembered to re-read".
        //
        // The bridge at the edge, like the write path two lines up: async verbs,
        // a synchronous shell, and the loop owns the terminal while this runs.
        // GIVEN THE MODEL, not ignoring it. `_ =>` here meant every refresh was
        // a boot, so everything the loader does not read - the browse pane, the
        // receipts, and on a failure the entire queue - reset to a default.
        // READ AGAIN RATHER THAN CAPTURED, because signing in is a reload and the
        // whole point of that one is that the name changed. Tolerating absence
        // for the boot's reason: this runs with no shell to refuse into either.
        // THROUGH THE SAME LOCAL FACTS THE BOOT APPLIES. Without this the
        // refresh answered with the loader's model and kept every local fact
        // from the state before it - so an offer taken on the key beside this
        // one went on being advertised until the console was restarted.
        reload: current => LocalFacts(
            ConsoleStart
                .LoadAsync(data, sessions.Read()?.PrincipalDisplay ?? "", current)
                .GetAwaiter()
                .GetResult(),
            client,
            sessions),
        // THE CHECKLIST IS READ WHEN THE PANE IS OPENED, not at boot: it is off
        // by default, and a request for a pane nobody opened is a request
        // nobody wanted.
        // THE ONE WRITE THAT WORKS BEFORE THERE IS A SESSION, and the reason
        // this console is worth drawing on a machine that has none. The bridge
        // at the edge again: async verbs, a synchronous shell, and the terminal
        // is provably free while these run.
        signIn: signIn,
        // ONE FLIGHT'S LOG, ON THE KEYPRESS. The boot reads a log only for a
        // flight still in the air - those are the only ones whose log can put a
        // row in the queue - so the detail modal reads its own.
        // STOPPING THE FLIGHT ON THE SCREEN, with the terminal free: the reason
        // is typed into $EDITOR and the write happens between sessions.
        groundFlight: (current, ask) => ConsoleGround.Ground(data, current, ask),

        // THE FILE, HANDED TO AN EDITOR WITH THE TERMINAL FREE. The path is
        // computed inside rather than passed, because ConfigurationFile is
        // where the XDG rule lives and a second answer here would be a second
        // place to get it wrong.
        configure: (current, ask) => current with
        {
            LastConfiguration = Gg.Console.ConsoleConfiguration.Edited(path: null, ask),
        },

        // TAKING WHAT THE PAGE SHOWED, through the same command line `gg config
        // accept` runs. The version is the console's, read off the model; the
        // fetch is fresh, and ConfigCommands.Accept refuses if the control
        // plane has changed its offer since - so a person cannot be given
        // something they did not read, whichever surface they used.
        takeOffered: (current, version) => current with
        {
            LastConfiguration = TakeOffered(client, sessions, version),
        },
        // THE VERIFICATION LINK, opened or copied. gg owns the terminal it is
        // drawn in, so it can be neither clicked nor selected - and reading a
        // long URL across to a browser by hand is the dead end the sign-in
        // modal exists to remove, one step further in.
        openUri: (current, uri) => ConsoleLink.Open(current, uri, Ran),
        copyUri: (current, uri) => ConsoleLink.Copy(current, uri, Ran),
        // THE RUNNER ON THIS MACHINE: start it, stop it, and keep the model's
        // picture of it current. Three verbs on one object, each named, so
        // EveryPortIsPassedTests can see all three.
        startRunner: runner.Start,
        stopRunner: runner.Stop,
        runnerHere: runner.Advance,
        // GOING AND WATCHING, which the modal has named since slice thirty-four
        // and could not do. It is here rather than in the console for the
        // reason its neighbours are: the child is this binary re-execed, and a
        // console that could name that invocation would be a console that can
        // act as a runner.
        //
        // WAITED FOR WITHOUT A DEADLINE, like flying by hand's child and unlike
        // the browser's. This one owns the terminal until the flight ends or
        // somebody stops it, and a grace period here would take the screen back
        // from a person mid-sentence.
        // GOING AND WATCHING, which the modal has named since slice thirty-four
        // and could not do. The connect runs HERE, between sessions, where the
        // terminal is free - which is what makes three calls to the control
        // plane and a handshake ordinary rather than an exception - and then
        // the pane draws from the buffer the watch fills.
        // GOING AND WATCHING. It STARTS the watch and comes straight back, so
        // the console is down for the instant a session takes to rebuild rather
        // than for however long a runner takes to answer. Every step, and the
        // reason if it fails, arrives in the pane.
        watchRunner: current => Gg.Console.ConsoleWatchRunner.Watch(
            current,
            start: (runnerId, flightId) =>
            {
                watched.Start(runnerId, flightId);
                return true;
            }),
        // FLYING BY HAND, which is `n new flight` with the terminal handed over.
        // What only this project can supply: this machine's labels, which gg the
        // child would be, and how to run it. The order - refuse before asking,
        // ask before creating - is ConsoleHandFlight's, where a test can reach
        // it.
        flyByHand: (current, ask) => ConsoleHandFlight.Fly(
            current,
            plan: () => data.PlanAsync().GetAwaiter().GetResult() is VerbResult.Plan plan
                ? plan.Value
                : throw new Gg.Client.NoEnvelopeException(
                    "No envelope has been applied, so there is nothing to plan against."),
            // WHAT THIS MACHINE ADVERTISES, read the same way `gg fly --hand`
            // reads it. The plan prices against the fleet, and a label some
            // other runner has is useless to a person at this keyboard.
            advertised: (Settings.Value("GG_RUNNER_LABELS", InForce.Configuration) ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ask: ask,
            self: Gg.Local.SelfInvocation.Current,
            start: info =>
            {
                using var child = Process.Start(info);
                child?.WaitForExit();
                return child?.ExitCode ?? -1;
            }),
        repositories: current => ConsoleRepositories.Read(data, current),
        // BOTH READS ON THE ONE KEY, because somebody who wants to know what
        // governs almost always wants to know where to change it, and a second
        // key for the second half would be a key nobody knew to press. The
        // estate read is second so a failing topology cannot cost the envelope.
        envelope: current => ConsoleEstate.Read(
            data, Airspace(), ConsoleEnvelope.Read(data, current)),

        // THE SAME ROOT THE VERBS AND THE PANE USE, so a person cannot be shown
        // one tree and have another written. The pull's own reload follows in
        // the loop, because the pane describes the tree this just rewrote.
        pullEstate: current => current with
        {
            LastEstate = Airspace() is { } pullInto
                ? ConsolePull.Pulled(
                    () => data.PullEstateAsync(pullInto).GetAwaiter().GetResult())
                : NoAirspace("pull"),
        },

        // BOTH ENDS OF ONE REPORT. The row gets a summary and the modal gets
        // the whole thing - and they come from ONE call, because two calls
        // would be two applies and a summary of a different one.
        applyEstate: current =>
        {
            if (Airspace() is not { } applyFrom)
            {
                return current with { LastEstate = NoAirspace("apply") };
            }

            var said = ConsoleApply.Applied(
                // DECLARING, BECAUSE THE QUESTION LISTED THEM. PaneText's apply
                // question names every undeclared name and the parent it would
                // use, so the `y` that reached this arm was an answer to that
                // too. Passing false here would make the question a lie.
                () => data.ApplyEstateAsync(applyFrom, declareNames: true)
                    .GetAwaiter().GetResult());

            return current with
            {
                ApplyOutcome = said,
                LastEstate = ConsoleApply.Summary(said),
            };
        },

        // THE SAME AGENT COMMAND AND THE SAME ENVELOPE THE COMPOSER GETS, so a
        // person who told gg which agent to run told it once, and the panel
        // shows the rules a document is being drafted toward.
        setAirspace: current => current with
        {
            LastEstate = Gg.Console.ConsoleAirspacePath.Set(
                path: null, typed: current.AirspacePathTyped),
        },

        draftEstate: current => current with
        {
            LastEstate = new Gg.Console.PtyDraftSession(
                Settings.Value("GG_TAKE_COMMAND", InForce.Configuration),
                envelope: () => ConsoleEnvelope.Read(data, new AppState()).Envelope)
                .Draft(Airspace()),
        },
        browser: new Gg.Console.ConfiguredWorkBrowser(readers))
        .Run(initial);

    // Demo/verification hook: prove the surviving model is the whole truth.
    var dumpPath = Environment.GetEnvironmentVariable("GG_STATE_DUMP");
    if (!string.IsNullOrEmpty(dumpPath))
    {
        File.WriteAllText(dumpPath, AppStateJson.Serialize(final));
    }
    return 0;
}

static int PrintVersion()
{
    // All three, not just the binary. A runner evaluating facts against a
    // vocabulary the control plane has moved past gives a silently wrong
    // answer, so the version that reveals it is printed alongside the others.
    Console.WriteLine($"gg                {GgVersions.Binary}");
    Console.WriteLine($"protocol          {GgVersions.Protocol}");
    Console.WriteLine($"fact vocabulary   {GgVersions.FactVocabulary}");
    return 0;
}

static async Task<int> AuthAsync(Func<AuthCommands, Task<int>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    var commands = new AuthCommands(
        new ControlPlaneClient(http),
        new FileSessionStore(),
        new StandardConsoleWriter(),
        new SystemClock(),
        (span, token) => Task.Delay(span, token));

    try
    {
        return await run(commands);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return 69;   // EX_UNAVAILABLE: the service will not serve this version
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine($"Could not reach the control plane at {baseAddress}: {failure.Message}");
        return ExitCodes.Unavailable;
    }
}

/// <summary>
/// `gg fly --hand`: open the flight and hand this terminal to the person.
/// </summary>
/// <remarks>
/// <b>Wiring only.</b> The order, the refusal and the three outcomes live in
/// <see cref="FlyByHandCommand"/>, where a test can reach them. What is here is
/// what only this project can supply: the control plane's address, this
/// machine's session, its own runner slot, and the attended executor.
/// </remarks>
/// <summary>
/// Watches what a runner's flight is saying, from wherever this is running.
/// </summary>
/// <remarks>
/// <b>Wiring only, like every other verb here.</b> The order, the refusals and
/// the sentence for each live in <see cref="WatchARunner"/>, where a test can
/// reach them. What is here is what only this project can supply: the control
/// plane's address, this machine's session, and the pinned keys file.
/// </remarks>
static async Task<int> WatchAsync(CliAction.RunnerWatch watch)
{
    var session = new FileSessionStore().Read();
    if (session is null)
    {
        return Fail("not signed in — run `gg login` first. Watching a runner is a person's action.");
    }

    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    // CTRL-C ENDS IT, and that is the only way it ends while the flight is
    // flying. Watching follows: the channel carries a request and a bounded
    // response, so following is asking again, and a person stops when they have
    // seen enough.
    using var stopping = new CancellationTokenSource();

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        stopping.Cancel();
    };

    var header = false;

    var watching = await new WatchARunner(
        new ControlPlaneClient(http),
        // STUN FROM THE ENVIRONMENT, for the runner's own reason: naming a
        // server in source would point every console at a service nobody chose.
        new ConsoleChannel(Gg.Runner.StunConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.StunConfiguration.Variable, InForce.Configuration)), TimeSpan.FromSeconds(20)))
        .WatchAsync(
            session.SessionToken,
            watch.RunnerId,
            new PinnedRunnerKeys(),
            watch.Lines,
            DateTimeOffset.UtcNow,
            // WRITTEN AS IT ARRIVES rather than collected and printed at the
            // end. A person watching a flight wants the screen to move; a tail
            // that appeared all at once when the flight landed would be a log
            // file with extra steps.
            write: line =>
            {
                if (!header)
                {
                    header = true;
                    Console.WriteLine();
                }

                Console.Out.WriteLine("  " + line);
                Console.Out.Flush();
            },
            follow: true,
            // WHAT IT IS DOING WHILE IT DOES IT. The connect can take a full
            // heartbeat interval - fifteen seconds on the deployed control
            // plane - and it used to spend all of it silent, so the ordinary
            // healthy case looked exactly like a hang. To stderr, because it is
            // progress rather than output: `gg runner watch > file` should get
            // the agent's words and not this.
            saying: step =>
            {
                Console.Error.WriteLine("  … " + step);
                Console.Error.Flush();
            },
            cancellationToken: stopping.Token);

    if (watching.Outcome is not WatchOutcome.Watching)
    {
        // ONE SENTENCE, AND IT ALREADY NAMES THE NEXT MOVE. Every outcome in
        // that enum sends somebody somewhere different, which is why there is an
        // enum rather than a string.
        return Fail(watching.Said);
    }

    Console.WriteLine();
    Console.WriteLine($"  — {watching.Said}");

    return 0;
}

static async Task<int> HandAsync(CliAction.Fly fly)
{
    var session = new FileSessionStore().Read();
    if (session is null)
    {
        // THE SAME WORDS `gg runner up` USES, because this does the same thing:
        // a hand-flight registers a runner on this machine, and registering a
        // runner is a person's action.
        return Fail("not signed in — run `gg login` first. Registering a runner is a person's action.");
    }

    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var client = new ControlPlaneClient(http);
    var commands = new FlightCommands(client, new FileSessionStore());

    var labels = (Settings.Value("GG_RUNNER_LABELS", InForce.Configuration) ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    return await FlyByHandCommand.RunAsync(
        fly,
        plan: async token => await client.GetPlanAsync(session.SessionToken, token)
            ?? throw new InvalidOperationException(
                "the control plane served no plan, so what this flight would need is unknown"),
        // WHAT THIS MACHINE ADVERTISES. The plan prices against the fleet, and a
        // label some other runner has is useless to a person at this keyboard.
        advertised: labels,
        open: _ => commands.FlyAsync(
            fly.Text, fly.Uri, provider: fly.Provider, id: fly.Id, repository: fly.Repository),
        hold: (flightId, token) => HoldAsync(baseAddress, http, session, labels, flightId, token),
        say: Console.WriteLine,
        // THE PERSON'S SESSION, ON A DOOR THAT ANSWERS TO ONE. Rule 8: the
        // launcher answers gates and the attended runner never does - and the
        // reason it never does is here rather than in a check, because this is
        // the process holding the session and the runner holds a runner token
        // it was handed. Neither is interchangeable with the other.
        gates: async flightNumber =>
        [
            .. (await client.GatesAsync(session.SessionToken)).Gates
                .Where(gate => string.Equals(
                    gate.FlightNumber, flightNumber, StringComparison.Ordinal)),
        ],
        answer: new ConsoleGateAnswer(),
        decide: async (flightNumber, obligation, outcome, reason) =>
        {
            // MEASURED, NOT CLAIMED. The observations say a person was asked
            // interactively and shown the evidence, and both are true HERE in a
            // way they are not on a scripted call - the gate was rendered to a
            // terminal a person was sitting at, seconds ago.
            var recorded = await commands.DecideAsync(
                flightNumber, obligation, outcome,
                new DecisionObservations { Interactive = true, EvidenceRendered = true },
                reason);

            return recorded is not null;
        });
}

/// <summary>
/// Runs the attended runner for one named flight, and goes home after it.
/// </summary>
/// <remarks>
/// <b>Its own credential slot, under the machine's name plus a suffix.</b>
/// <c>FileRunnerStore.PathFor</c> keys a runner's identity by name, so a
/// hand-flight sharing the fleet runner's slot would have the two overwrite
/// each other's token - and read-or-register keeps one host from appearing as
/// eleven runners, which is the defect that mechanism exists for.
/// </remarks>
static async Task<int> HoldAsync(
    string baseAddress, HttpClient http, StoredSession session,
    IReadOnlyList<string> labels, string flightId, CancellationToken cancellationToken)
{
    // NAMED, NOT SILENT. FromEnvironment answers null for an unconfigured
    // machine and the fleet runner treats that as "this host has no agent" - on
    // a hand-flight there is a person waiting at a terminal for one, so it is
    // said rather than discovered as a session that never starts.
    if (Environment.GetEnvironmentVariable(
            Gg.Runner.Execution.ExecutorConfiguration.BinaryVariable) is not { Length: > 0 } binary)
    {
        return Fail(
            $"this machine declares no agent — set {Gg.Runner.Execution.ExecutorConfiguration.BinaryVariable} "
          + "to the binary you want handed the flight.");
    }

    var name = Gg.Client.AttendedRunner.NameFor(Environment.MachineName);

    var registered = await RunnerIdentity.EnsureAsync(
        new FileRunnerStore(FileRunnerStore.PathFor(name)),
        async () =>
        {
            var fresh = await new ControlPlaneClient(http)
                .RegisterRunnerAsync(
                    session.SessionToken, name,
                    // MADE HERE, ON FIRST REGISTRATION, because that is the moment a
                    // console pins. The private half never leaves this machine.
                    publicKey: RunnerIdentityKey
                        .LoadOrCreate(RunnerIdentityKey.PathFor(name)).PublicKey);

            return new StoredRunner
            {
                RunnerId = fresh.RunnerId,
                RunnerToken = fresh.RunnerToken,
                // THE SAME THIRTY DAYS the fleet runner's slot gets. A shorter
                // life here would make a person re-register on a machine they
                // hand-fly from weekly, which is the friction read-or-register
                // exists to remove.
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };
        },
        DateTimeOffset.UtcNow);

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), registered.RunnerId, registered.RunnerToken, labels,
        TimeSpan.Zero,
        new LocalCredentialResolver(new FileCredentialStore()),
        new Gg.Runner.Workspace(
            Gg.Runner.Vcs.VcsConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.Vcs.VcsConfiguration.HostsVariable, InForce.Configuration)), new Gg.Runner.Vcs.WorkingTreeRoot()),
        cancellationToken,
        destinations: Gg.Runner.Vcs.DestinationConfiguration.FromEnvironment(
            api => new HttpClient { BaseAddress = new Uri(api) }),
        // THE OTHER EXECUTOR, and the only line that decides a person rather
        // than an agent does the work. The SAME binary the fleet runs, from the
        // same variable: a hand-flight and a fleet flight run the same agent,
        // and a second way to name it would be a second answer to which one
        // this machine has.
        executor: new Gg.Runner.Execution.AttendedExecutor(
            binary,
            Gg.Local.IntentConfiguration.FromEnvironment(
            Settings.Value(Gg.Local.IntentConfiguration.ReadersVariable, InForce.Configuration),
            Settings.Value(Gg.Local.IntentConfiguration.ServedVariable, InForce.Configuration)),
            secretFor: locator => new FileCredentialStore().Read(locator),
            self: Gg.Local.SelfInvocation.Current),
        flightId: flightId,
        // A HAND-FLOWN FLIGHT SPENDS THE SAME ALLOWANCE, so it reports one.
        // The executor above has no stream to count and the fact carries no
        // tokens for it - but the machine's own ledger reads every transcript
        // on the disk, including the one a person is sitting in front of, and
        // that is exactly the spending a reserve exists to protect.
        allowance: Allowance(),
        // THE ONE READER, HANDED ACROSS. Gg.Runner cannot see Gg.Client - the
        // runner is treated as hostile and the reference graph keeps them
        // apart - so this project, which is the only one that sees both, passes
        // it. A second implementation of "what did the person decide" would be
        // a second size bound and a second set of three diagnoses to drift from
        // these.
        returns: (tree, flight) => TakeoverReturnReader.Read(
            TakeoverReturnReader.PathIn(tree), flight));
}

static async Task<int> RunnerUpAsync()
{
    var baseAddress = ControlPlaneAddress();

    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    // A POOL MEMBER STARTS HERE, and it has no session because nobody is
    // present when a container is warmed. It finds a single-use nonce in its
    // environment, put there by the resident runner that created it, and
    // exchanges that for an identity of its own.
    //
    // This is the branch that lets a member run a flight at all. Before it,
    // `gg runner up` refused without a session, so the only way to warm a
    // working member was to bake a developer's session into an image - which
    // lasts twelve hours and carries their whole surface.
    if (Environment.GetEnvironmentVariable(
            Gg.Runner.Pools.MemberBootstrap.NonceVariable) is { Length: > 0 } nonce)
    {
        return await MemberUpAsync(http, baseAddress, nonce);
    }

    var session = new FileSessionStore().Read();
    if (session is null)
    {
        return Fail("not signed in — run `gg login` first. Registering a runner is a person's action.");
    }

    // A person registers the runner; the runner then holds only the credential
    // that comes back. The developer session never reaches the runner process.
    //
    // READ-OR-REGISTER, the same decision `gg runner maintain` makes. This used
    // to register unconditionally on every start, so one host showed as eleven
    // runners in `gg runners` with ten of them permanently offline - one per
    // restart - and a machine could not come back from a reboot without
    // somebody signed in.
    var registered = await RunnerIdentity.EnsureAsync(
        new FileRunnerStore(FileRunnerStore.PathFor(Environment.MachineName)),
        async () =>
        {
            var fresh = await new ControlPlaneClient(http)
                .RegisterRunnerAsync(
                    session.SessionToken, Environment.MachineName,
                    publicKey: RunnerIdentityKey
                        .LoadOrCreate(RunnerIdentityKey.PathFor(Environment.MachineName))
                        .PublicKey);

            return new StoredRunner
            {
                RunnerId = fresh.RunnerId,
                RunnerToken = fresh.RunnerToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };
        },
        DateTimeOffset.UtcNow);

    // WHAT THE CONTROL PLANE OFFERS, TAKEN BEFORE ANYTHING IS COMPOSED. Every
    // value below is read once into a local and handed to the loop, so a file
    // written later in the run changes nothing until the process restarts -
    // which is exactly why this is here and not on the beat that carries it. A
    // machine brought up fresh is configured before it claims anything.
    //
    // ONE HEARTBEAT, because that is the only way a runner can be handed an
    // offer: the read a person uses answers a developer session and refuses a
    // runner credential, deliberately. This beat declares no labels, which
    // costs nothing - a claim carries its own, and the loop re-declares on
    // every beat after this one.
    var inForce = InForce.Configuration;

    try
    {
        var beat = await new Gg.Runner.RunnerProtocolClient(
                new HttpClient { BaseAddress = new Uri(baseAddress) }, registered.RunnerToken)
            .HeartbeatAsync(registered.RunnerId, [], CancellationToken.None);

        var decided = OfferedAtStartup.Decide(
            beat.Offered, inForce, Gg.Local.ConfigurationFile.DefaultPath());

        if (decided.Note is { Length: > 0 } note)
        {
            // STDERR, and the reader is a log. Nobody is at a runner, so an
            // offer it could not take reaches an operator here or nowhere.
            Console.Error.WriteLine($"gg: {note}");
        }

        if (decided.Write is { } take)
        {
            Gg.Local.ConfigurationFile.Write(take);
            Console.Error.WriteLine(
                $"gg: took offered configuration {beat.Offered!.Version}, written to "
              + Gg.Local.ConfigurationFile.DefaultPath());
        }

        inForce = decided.InForce;
    }
    catch (HttpRequestException unreachable)
    {
        // AN ENHANCEMENT TO BRING-UP, NOT A DEPENDENCY OF IT. A runner that
        // refused to start because it could not ask what was offered would turn
        // one control-plane outage into a fleet that does not come back, which
        // is far worse than composing from a file one version behind.
        Console.Error.WriteLine(
            "gg: could not ask what this control plane offers, so this runner starts on "
          + $"what is already in force: {unreachable.Message}");
    }
    catch (IOException unwritable)
    {
        // The same argument one step along: a disk that will not take the file
        // is a reason to carry on with the old values, not to stay down.
        Console.Error.WriteLine(
            "gg: could not write offered configuration, so this runner starts on what is "
          + $"already in force: {unwritable.Message}");
    }

    var labels = (Settings.Value("GG_RUNNER_LABELS", inForce) ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var holdFor = int.TryParse(
        Settings.Value("GG_RUNNER_HOLD_SECONDS", inForce), out var seconds)
        ? TimeSpan.FromSeconds(seconds)
        : TimeSpan.FromSeconds(10);

    // Ctrl-C stops the loop. A KILL does not, and that is the interesting case:
    // the lease outlives the process and expires on the control plane's clock.
    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

    // The runner resolves credentials from the SAME local store gg credential
    // add wrote to. The reference travels through the control plane; the value
    // never leaves this machine, and the two halves are joined here because
    // this is the only project that can see both.
    // Which providers this runner serves, and where they live. Deployment
    // knowledge, so it is configured rather than compiled in: gg is public and
    // distributed, and which forge a tenant uses is the control plane's
    // business. A provider nobody configured is a declared capability gap.
    var workspace = new Gg.Runner.Workspace(
        Gg.Runner.Vcs.VcsConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.Vcs.VcsConfiguration.HostsVariable, inForce)), new Gg.Runner.Vcs.WorkingTreeRoot());

    // Where this runner may LAND work, which is a second declaration on purpose.
    // A runner configured to read and not to write cannot write - there is no
    // adapter for it to reach. Absent is the ordinary state.
    var destinations = Gg.Runner.Vcs.DestinationConfiguration.FromEnvironment(
        api => new HttpClient { BaseAddress = new Uri(api) });

    // AND WHERE IT MAY WRITE TO A TRACKER, which is a third declaration for
    // the reason the second one is a second: reading a backlog and changing
    // one are different permissions on different credentials. A runner told
    // about no tracker write api holds no sink, so "no destination, no write"
    // is true because there is no object rather than because a check said so.
    //
    // NOTHING BUILT ONE FOR A WHOLE SLICE. WiqlWorkItemSink shipped, was
    // walked against a real tracker, and was reachable from no product code -
    // an unbuilt feature that reads as a finished one, which is the shape
    // S7.4-02 recorded the last time.
    var trackers = Gg.Runner.Intent.TrackerConfiguration.FromEnvironment(
        api => new HttpClient(),
        // THROUGH THE ONE READER, so a tracker-apis line in the configuration
        // file reaches the sinks. Read straight from the environment this
        // would be the stun-servers defect again, one variable over.
        apis: Settings.Value(Gg.Runner.Intent.TrackerConfiguration.ApisVariable),
        secretFor: destination => new FileCredentialStore().Read(destination));

    // WHICH AGENT THIS MACHINE HAS, and none is a real answer. Until this line
    // existed the runner was handed no executor at all, so `gg runner serve`
    // built a loop that could not invoke anything and no flight in the product
    // ever ran an agent - registered is not invoked, on the verb the whole slice
    // is about. The host probes whatever comes back before it claims any work.
    // WHERE A TOOL SERVER'S CREDENTIAL COMES FROM, and the only place this
    // process hands one over. The same store `gg credential add` writes; the
    // secret goes into the server's own environment and never into the agent's.
    var executor = Gg.Runner.Execution.ExecutorConfiguration.FromEnvironment(
        secretFor: locator => new FileCredentialStore().Read(locator));

    // WHERE THIS RUNNER IS RUNNING, for any console that wants to look. A
    // runner outlives the console that started it - reparented to init a moment
    // after that window closes - so a handle answers "did I start this" and
    // this answers the question a person is actually asking.
    // THE KEY THIS MACHINE CAN BE REACHED ON, loaded once and used twice: to
    // open what a console seals, and to make sure the control plane KNOWS it.
    var identity = RunnerIdentityKey
        .LoadOrCreate(RunnerIdentityKey.PathFor(Environment.MachineName));

    // AND OFFERED, because registration is read-or-register. A runner with a
    // stored credential never registers again, so a key made locally after that
    // first registration is never presented to anybody - which left every runner
    // registered before keys existed permanently unreachable, holding one on its
    // own disk. Idempotent: the control plane answers 204 whether it set the key
    // or already had this one.
    switch (await new Gg.Runner.RunnerProtocolClient(
                new HttpClient { BaseAddress = new Uri(baseAddress) }, registered.RunnerToken)
            .OfferKeyAsync(registered.RunnerId, identity.PublicKey))
    {
        case Gg.Runner.KeyOfferResult.Refused:
            // A DIFFERENT KEY IS REGISTERED, and this runner cannot fix that.
            // Consoles pinned the other one; bringing this machine back under a
            // key it holds is a person registering it again, which mints an
            // identity a console will notice rather than one that changed under
            // it. Said and carried on: the runner still takes work.
            Console.Error.WriteLine(
                $"this runner is registered under a different key than the one at "
              + $"{RunnerIdentityKey.PathFor(Environment.MachineName)}, so nobody can reach it "
              + "by hand. Its own private half is gone or this is a second machine using the "
              + "name. `gg runner retire` then `gg runner up` registers it again, which a "
              + "console will see as a new key rather than a changed one.");
            break;

        default:
            break;
    }

    var pidFile = new RunnerPidFile(RunnerPidPath());
    pidFile.Write(Environment.ProcessId);

    try
    {
        return await Gg.Runner.RunnerHost.RunAsync(
            new Uri(baseAddress), registered.RunnerId, registered.RunnerToken, labels, holdFor,
            new LocalCredentialResolver(new FileCredentialStore()), workspace, stopping.Token,
            destinations: destinations, trackers: trackers, executor: executor,
            allowance: Allowance(),
            // WHAT MAKES THIS RUNNER REACHABLE, handed across for the reason the
            // takeover reader is: Gg.Runner cannot see Gg.Client, and this
            // project is the only one that sees both. The SAME key this machine
            // registered with a moment ago - a second one would be a runner
            // whose console pinned a key it can no longer open anything with.
            identityKey: identity.ForOpeningWhatWasSealedToThisRunner(),
            // WHERE THIS MACHINE ASKS WHAT IT LOOKS LIKE FROM OUTSIDE, from the
            // environment for the reason the trackers and the hosts are: naming
            // one in source would point every runner in every deployment at a
            // service nobody chose, and the well-known ones belong to companies
            // this binary may not name. Empty is a real answer -
            // host candidates work between machines that can already reach each
            // other - and TURN is S34.Q-04, still open.
            stunServers: Gg.Runner.StunConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.StunConfiguration.Variable, inForce)),
            // A NEW OFFER ENDS THIS PROCESS SO THE NEXT ONE TAKES IT. Nothing
            // is applied here: everything above was composed already, and the
            // startup path is the one place an offer lands. Stopping is how
            // that path gets run again.
            //
            // ONLY IF A RESTART WOULD ACTUALLY WRITE SOMETHING, which is the
            // guard against an infinite restart loop: a runner that stopped for
            // ANY offer would meet a directed one it may never take, exit,
            // boot, meet it again and exit for ever. Decide is asked the same
            // question the next startup will ask, so "worth restarting for" and
            // "would be taken" cannot come apart.
            //
            // The loop only reports on an IDLE beat, so this never ends a
            // process holding somebody's flight.
            offered: carried =>
            {
                if (OfferedAtStartup.Decide(
                        carried, inForce, Gg.Local.ConfigurationFile.DefaultPath()).Write is null)
                {
                    return;
                }

                Console.Error.WriteLine(
                    $"gg: offered configuration {carried.Version} is not what this runner is "
                  + "running on, and nothing is applied to a running loop. Stopping so the "
                  + "next start takes it.");

                stopping.Cancel();
            });
    }
    finally
    {
        // TAKEN BACK, or the next console reads a runner that stopped hours
        // ago. A kill -9 leaves it behind, which is why every reader checks the
        // pid is alive before believing it.
        pidFile.Clear();
    }
}

/// <summary>
/// Serve one tracker's work items to the agent that started this process.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CREDENTIAL IS RESOLVED HERE, WHICH IS THE WHOLE POINT.</b> An
/// external tool server can only be handed a secret through the config that
/// launches it, and that config is an argument to the agent - readable by every
/// <c>ps</c> on the host. This process is handed a NAME instead and reads the
/// store itself, so the secret exists only in this address space and only after
/// the agent has already started us.
/// </para>
/// <para>
/// <b>An unresolvable credential is a tool that says so, not a process that
/// dies.</b> A server that exits before its first line is a server the agent
/// reports as never initialized, and the reason - an expired credential on this
/// host - would reach nobody. So the source is built with no secret, the
/// tracker answers 401, and the agent is told a sentence it can stop on.
/// </para>
/// <para>
/// <b>Nothing here prints.</b> Stdout is the protocol; the resolution failure
/// above is exactly the kind of thing that wants a log line, and must not have
/// one.
/// </para>
/// </remarks>
static async Task<int> RunnerReadAsync(CliAction.RunnerRead read)
{
    string? secret = null;

    if (read.Credential is { Length: > 0 } locator)
    {
        var resolved = await new LocalCredentialResolver(new FileCredentialStore())
            .ResolveAsync(
                new Gg.Contracts.CredentialReference
                {
                    Kind = "local",
                    Locator = locator,
                    Identity = read.Provider,
                    Scopes = ["read"],
                });

        secret = resolved is Gg.Runner.CredentialResolution.Resolved granted ? granted.Secret : null;
    }

    using var client = new HttpClient();

    return await WorkItemToolServer.RunAsync(
        System.Console.In,
        System.Console.Out,
        new Gg.Runner.Intent.WiqlWorkItemSource(read.Host, secret, client));
}

/// <summary>
/// A pool member coming up: redeem the nonce, then run as an ordinary runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-or-redeem, the same decision every runner makes.</b> A container
/// restarts, and a member that redeemed on every start would find its nonce
/// spent and never come up again — so a stored credential answers whenever
/// there is one, and the nonce is only for the first breath.
/// </para>
/// <para>
/// <b>The labels come from the credential, not from the environment.</b> What a
/// member may advertise was decided control-plane-side from the strategy; a
/// variable saying the same thing would be a second source of truth for the one
/// value that must not be a runner's to choose.
/// </para>
/// </remarks>
static async Task<int> MemberUpAsync(HttpClient http, string baseAddress, string nonce)
{
    // KEYED ON THE CONTAINER, so a restart finds what the first start stored.
    var store = new FileRunnerStore(FileRunnerStore.PathFor(Environment.MachineName));

    StoredRunner identity;
    try
    {
        identity = await RunnerIdentity.EnsureAsync(
            store,
            async () =>
            {
                var issued = await new ControlPlaneClient(http).RedeemMemberAsync(nonce)
                    ?? throw new InvalidOperationException(
                        "this member's nonce buys nothing: it was never minted, it has expired, "
                      + "or it has already been redeemed. A nonce is spent exactly once, and a "
                      + "member cannot mint itself another - the pool has to warm a new one.");

                return new StoredRunner
                {
                    RunnerId = issued.RunnerId,
                    RunnerToken = issued.RunnerToken,
                    Labels = issued.Labels,
                    ExpiresAt = issued.ExpiresAt,
                };
            },
            DateTimeOffset.UtcNow);
    }
    catch (InvalidOperationException refusal)
    {
        // A CLEAN REFUSAL rather than a stack trace. This lands in a
        // container's log, which is where somebody looks when a member is warm
        // and claims nothing.
        return Fail(refusal.Message);
    }

    var holdFor = int.TryParse(
        Settings.Value("GG_RUNNER_HOLD_SECONDS", InForce.Configuration), out var seconds)
        ? TimeSpan.FromSeconds(seconds)
        : TimeSpan.FromSeconds(10);

    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

    var workspace = new Gg.Runner.Workspace(
        Gg.Runner.Vcs.VcsConfiguration.FromEnvironment(
            Settings.Value(Gg.Runner.Vcs.VcsConfiguration.HostsVariable, InForce.Configuration)), new Gg.Runner.Vcs.WorkingTreeRoot());

    var destinations = Gg.Runner.Vcs.DestinationConfiguration.FromEnvironment(
        api => new HttpClient { BaseAddress = new Uri(api) });

    // AND WHERE IT MAY WRITE TO A TRACKER, which is a third declaration for
    // the reason the second one is a second: reading a backlog and changing
    // one are different permissions on different credentials. A runner told
    // about no tracker write api holds no sink, so "no destination, no write"
    // is true because there is no object rather than because a check said so.
    //
    // NOTHING BUILT ONE FOR A WHOLE SLICE. WiqlWorkItemSink shipped, was
    // walked against a real tracker, and was reachable from no product code -
    // an unbuilt feature that reads as a finished one, which is the shape
    // S7.4-02 recorded the last time.
    var trackers = Gg.Runner.Intent.TrackerConfiguration.FromEnvironment(
        api => new HttpClient(),
        // THROUGH THE ONE READER, so a tracker-apis line in the configuration
        // file reaches the sinks. Read straight from the environment this
        // would be the stun-servers defect again, one variable over.
        apis: Settings.Value(Gg.Runner.Intent.TrackerConfiguration.ApisVariable),
        secretFor: destination => new FileCredentialStore().Read(destination));

    // WHERE A TOOL SERVER'S CREDENTIAL COMES FROM, and the only place this
    // process hands one over. The same store `gg credential add` writes; the
    // secret goes into the server's own environment and never into the agent's.
    var executor = Gg.Runner.Execution.ExecutorConfiguration.FromEnvironment(
        secretFor: locator => new FileCredentialStore().Read(locator));

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), identity.RunnerId, identity.RunnerToken, identity.Labels, holdFor,
        new LocalCredentialResolver(new FileCredentialStore()), workspace, stopping.Token,
        destinations: destinations, trackers: trackers, executor: executor,
        allowance: Allowance());
}

/// <summary>
/// This machine's allowance, read now.
/// </summary>
/// <remarks>
/// <b>Refused by name when nothing is configured.</b> An allowance nobody
/// named is one nobody agreed to lend, so there is no number to invent - and a
/// refusal that did not say which setting was missing would send somebody
/// reading their own file to work out which of two lines to add.
/// </remarks>
static VerbResult AllowanceNow()
{
    var reporter = Allowance()
        ?? throw new Gg.Client.ConfigurationRefused(
            "This machine names no allowance, so there is nothing to measure. Set "
          + "`allowance` to whatever you call the subscription it spends from - it is a "
          + "name you choose, never an account - and `allowance-limits` to that plan's "
          + "ceilings, like session=88000,week=2400000.");

    return new VerbResult.Allowance(
        reporter.ReadAsync(DateTimeOffset.UtcNow).GetAwaiter().GetResult()
        ?? throw new Gg.Client.ConfigurationRefused(
            "This machine names an allowance and its transcripts say nothing was spent."));
}

/// <summary>
/// What this machine has spent, or nothing when it names no allowance.
/// </summary>
/// <remarks>
/// <b>Through the one reader</b>, like the trackers and the hosts: a value in
/// the configuration file has to reach this as surely as a variable does, and
/// reading the environment straight would be the stun-servers defect again.
/// </remarks>
static Gg.Runner.AllowanceReporter? Allowance() =>
    Gg.Runner.AllowanceReporter.For(
        Settings.Value("GG_ALLOWANCE", InForce.Configuration),
        Settings.Value("GG_ALLOWANCE_LIMITS", InForce.Configuration));

static async Task<int> RunnerMaintainAsync(string pool)
{
    var baseAddress = ControlPlaneAddress();

    // THE RUNNER'S OWN CREDENTIAL FIRST. A session lasts twelve hours and a
    // runner token thirty days, and this registers on every start - so a host
    // that could only present a session would fail to restart after half a day,
    // on a machine with nobody at it. RunnerRegistry designed the separation
    // for exactly this: "the runner's lifetime is its own."
    var runners = new FileRunnerStore();
    var held = runners.Usable(DateTimeOffset.UtcNow);

    // The scope-enforcing proxy, or nothing. A resident runner with no
    // endpoint is not a resident, and guessing a socket path here would be
    // the exact reach § 12 forbids - refused loudly, naming the variable.
    var configuration = Gg.Runner.Pools.PoolConfiguration.FromEnvironment(
        Settings.Value("GG_POOL_ENDPOINT", InForce.Configuration));
    if (configuration is null)
    {
        return Fail("GG_POOL_ENDPOINT is not set. The resident runner acts only through the "
                  + "scope-enforcing proxy; point this at it (never at the raw socket).");
    }

    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    string runnerToken;

    // THE SAME DECISION `gg runner up` makes, and it is shared now rather than
    // written twice. These two had drifted: this one read-or-registered and
    // that one registered every start, which is how one host became eleven
    // rows in `gg runners`.
    //
    // ON ITS HISTORICAL PATH. This is the only file any version of gg has
    // written, and on a live pool host it holds this service's thirty-day
    // token. Moving it would make the next start find nothing and refuse,
    // because a maintain start without a credential needs a person signed in -
    // and on a pool host nobody is.
    StoredRunner identity;
    try
    {
        identity = await RunnerIdentity.EnsureAsync(
        runners,
        async () =>
        {
            var signedIn = new FileSessionStore().Read();
            if (signedIn is null)
            {
                // NAMES THE CADENCE. "Not signed in" on a host that ran
                // yesterday reads like a broken machine rather than a
                // credential reaching the end of its life. Nothing renews a
                // runner token - the protocol's renew is for a LEASE - so this
                // is a person's action every thirty days, by design.
                throw new InvalidOperationException(
                    "no usable runner credential, and not signed in. A pool host runs on its own "
                  + "runner token, which lasts thirty days and cannot be renewed - so a person signs "
                  + "in once to mint a new one: run `gg login`, then start this again. Registering a "
                  + "runner is a person's action.");
            }

            var fresh = await new ControlPlaneClient(http)
                .RegisterRunnerAsync(
                    signedIn.SessionToken, Environment.MachineName + ":maintain",
                    // ONE KEY PER RUNNER NAME. This host's `runner up` has its own; sharing
                    // would make a console pinning "this runner" pin "this host".
                    publicKey: RunnerIdentityKey
                        .LoadOrCreate(
                            RunnerIdentityKey.PathFor(Environment.MachineName + ":maintain"))
                        .PublicKey);

            return new StoredRunner
            {
                RunnerId = fresh.RunnerId,
                RunnerToken = fresh.RunnerToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };
        },
            DateTimeOffset.UtcNow);
    }
    catch (InvalidOperationException refusal)
    {
        // A CLEAN REFUSAL, not a stack trace. This is the every-thirty-days
        // path and it lands on somebody's console; the sentence is the whole
        // point of it.
        return Fail(refusal.Message);
    }

    runnerToken = identity.RunnerToken;

    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

    var protocol = new Gg.Runner.RunnerProtocolClient(http, runnerToken);
    var adapter = new Gg.Runner.Pools.DockerPoolAdapter(
        new HttpClient { BaseAddress = new Uri(configuration.Endpoint) });

    // NARRATED, because this loop reported nothing at all. A pull point that
    // crash-looped for hours looked exactly like one quietly doing its job, and
    // the pool it manages grew to 196 dead members with nobody told.
    var loop = new Gg.Runner.Pools.MaintainLoop(
        protocol, adapter, new Gg.Runner.SystemClock(), Task.Delay,
        narrate: Console.Error.WriteLine,

        // WHERE A MEMBER ANSWERS TO, which is usually this host's own control
        // plane and is not always: a container's 127.0.0.1 is the container.
        // What it may ADVERTISE is not here - that comes back with the
        // credential it redeems, decided control-plane-side from the strategy.
        controlPlane: Gg.Runner.Pools.MemberBootstrap.ControlPlaneFor(
            baseAddress,
            Environment.GetEnvironmentVariable(
                Gg.Runner.Pools.MemberBootstrap.ReachableAsVariable)));

    return await loop.RunAsync(pool, stopping.Token);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return ExitCodes.Refused;
}

/// <summary>
/// `gg update`. What shape this install is, and the command that would move it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Synchronous, and that is the rule showing through the signature.</b>
/// There is nothing to await because there is nothing to fetch, write or spawn:
/// gg replaces no binary of its own. When the control plane can say what is
/// current - the one channel independent of the feed - that answer travels in
/// as a value, so this stays a function of facts rather than a thing that
/// reaches.
/// </para>
/// <para>
/// <b>Exit zero either way.</b> Being behind is reported, never blocking; the
/// protocol floor already refuses with a 426 and that stays the only thing that
/// does. A non-zero exit here turns "there is a newer gg" into a failed build
/// on somebody else's machine.
/// </para>
/// </remarks>
static async Task<int> UpdateReportAsync(bool json)
{
    // THE ONE CHANNEL THAT IS NOT THE FEED. Asking nuget.org what is current
    // would be asking the party a stolen key lets lie: `dotnet tool update`
    // with no version takes whatever was pushed last, and repository signing
    // proves the pipeline rather than the publisher.
    //
    // CurrentVersionAsync returns null for every way this can fail, on purpose,
    // and UpdateAdvice renders null as an absence rather than as currency. So
    // the control plane being down costs a person the ANSWER and never gives
    // them a wrong one.
    using var http = new HttpClient { BaseAddress = new Uri(ControlPlaneAddress()) };
    var current = await new ControlPlaneClient(http).CurrentVersionAsync();

    var advice = Gg.Local.UpdateAdvice.For(Gg.Local.InstallShape.Current, current);

    if (json)
    {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
            new UpdateReportJson(
                advice.Shape.Kind.ToString(),
                advice.Shape.ToolPath,
                GgVersions.Binary,
                advice.Current,
                advice.Summary,
                [.. advice.Commands]),
            UpdateJsonContext.Default.UpdateReportJson));

        return ExitCodes.Ok;
    }

    Console.WriteLine(advice.Summary);

    foreach (var command in advice.Commands)
    {
        Console.WriteLine();
        Console.WriteLine("  " + command);
    }

    return ExitCodes.Ok;
}

/// <summary>What `gg update --json` emits.</summary>
/// <param name="Shape">Which install this is.</param>
/// <param name="ToolPath">Where the tool lives, when it is one.</param>
/// <param name="Installed">The version running now.</param>
/// <param name="Current">What is current, or null where that could not be established.</param>
/// <param name="Summary">The same sentence the text form prints.</param>
/// <param name="Commands">What to run, which may legitimately be empty.</param>
internal sealed record UpdateReportJson(
    string Shape,
    string? ToolPath,
    string Installed,
    string? Current,
    string Summary,
    IReadOnlyList<string> Commands);

/// <summary>
/// Source-generated, because everything here must stay AOT-publishable.
/// </summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateReportJson))]
internal sealed partial class UpdateJsonContext : System.Text.Json.Serialization.JsonSerializerContext;

/// <summary>
/// The processes on this machine, for the console's runner.
/// </summary>
/// <remarks>
/// <b>Here rather than in Gg.Console, because it is the one thing in this that
/// only the composition root may do.</b> A pid off a file is checked before it
/// is reported and before it is signalled, and the name is checked with it: a
/// reused pid belonging to something else is a stranger this console must not
/// kill.
/// </remarks>
internal sealed class LocalProcesses : Gg.Console.IRunnerProcesses
{
    /// <summary>How long a shutdown waits before saying it did not happen.</summary>
    private const int Grace = 5000;

    public bool Alive(int pid) => Find(pid) is not null;

    public bool Stop(int pid)
    {
        if (Find(pid) is not { } process)
        {
            return false;
        }

        try
        {
            using (process)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(Grace);

                return process.HasExited;
            }
        }
        catch (Exception failure) when (failure is InvalidOperationException
                                            or NotSupportedException
                                            or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The process under this pid, if it is one of ours.
    /// </summary>
    /// <remarks>
    /// The name is the check. A pid file left behind by a kill -9 names a number
    /// the operating system will hand to somebody else, and signalling that is
    /// how a console comes to stop a database.
    /// </remarks>
    private static Process? Find(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);

            if (process.HasExited
                || !process.ProcessName.StartsWith("gg", StringComparison.Ordinal))
            {
                process.Dispose();

                return null;
            }

            return process;
        }
        catch (Exception failure) when (failure is ArgumentException
                                            or InvalidOperationException
                                            or NotSupportedException)
        {
            return null;
        }
    }
}
