namespace Gg.Local;

/// <summary>
/// What an operator chose, on this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value is the STRING its variable carries, not a parsed shape.</b>
/// <c>VcsConfiguration.FromEnvironment(string? declared)</c> and its five
/// siblings already take their declaration as text, and that optional parameter
/// is the seam this type plugs into — so a file value reaches the existing
/// parser, the existing refusal message and the existing tests untouched. A
/// parsed shape here would have to be rendered back to feed that seam, which is
/// one value in two representations and a second thing to get wrong.
/// </para>
/// <para>
/// <b>Every member is optional, and absent is not the same as blank.</b> Absent
/// means the operator chose nothing and the environment or the default answers.
/// Blank is refused: a person who typed <c>""</c> wrote a value, the reader
/// would see silence, and nothing on any screen would say the line did nothing.
/// </para>
/// <para>
/// <b>What is deliberately NOT here.</b> The three path roots
/// (<c>XDG_CONFIG_HOME</c>, <c>XDG_STATE_HOME</c>, <c>XDG_CACHE_HOME</c>),
/// because this file's own location is computed from the first of them — a
/// value inside it could not be read before it was needed. And the
/// per-invocation signals (<c>GG_STATE_DUMP</c>, <c>GG_MEMBER_NONCE</c>,
/// <c>GG_MEMBER_CONTROL_PLANE</c>, <c>GG_IMAGE_DIGEST</c>), which are one
/// process telling another something rather than settings anybody chose.
/// </para>
/// <para>
/// <b>And no secret.</b> This file names which credential to use and never the
/// credential — <c>IntentReader.Locator</c>'s rule, one level up. Secrets stay
/// in <c>credentials/*.secret</c> at 0600.
/// </para>
/// </remarks>
public sealed record Configuration
{
    /// <summary>The control plane this machine reads and writes.</summary>
    public string? ControlPlane { get; init; }

    /// <summary>The editor a handoff gives the terminal to.</summary>
    public string? Editor { get; init; }

    /// <summary>What `t` starts to hand somebody a flight's tree.</summary>
    public string? TakeCommand { get; init; }

    /// <summary>Trackers this binary reads work items from itself.</summary>
    public string? IntentHosts { get; init; }

    /// <summary>Trackers read by a tool server somebody installed.</summary>
    public string? IntentReaders { get; init; }

    /// <summary>Which forge each provider key clones from.</summary>
    public string? VcsHosts { get; init; }

    /// <summary>Where a proposal is opened, per provider key.</summary>
    public string? DestinationApis { get; init; }

    /// <summary>
    /// Where an admitted change to a work item is written, per destination id.
    /// </summary>
    /// <remarks>
    /// <b>A second declaration beside <see cref="DestinationApis"/>, and for
    /// its reason.</b> Reading a tracker and changing one are different
    /// permissions on different credentials, so a machine that reads a backlog
    /// does not thereby write to it: absent means this runner holds no sink
    /// and can perform nothing, however an envelope is written. The credential
    /// is not here — this names which tracker, never how to authenticate to
    /// one.
    /// </remarks>
    public string? TrackerApis { get; init; }

    /// <summary>The agent binary a runner invokes.</summary>
    public string? ExecutorBinary { get; init; }

    /// <summary>The labels this machine's runner advertises.</summary>
    public string? RunnerLabels { get; init; }

    /// <summary>How long a lease claim waits, in seconds.</summary>
    /// <remarks>
    /// A number rather than the text its variable carries, unlike every member
    /// above it. There is no <c>FromEnvironment</c> seam for this one — the
    /// composition root parses it inline — so there is no string shape to
    /// preserve, and a number is what a person editing the file would write.
    /// </remarks>
    public int? RunnerHoldSeconds { get; init; }

    /// <summary>The scope-enforcing proxy a pool maintainer works through.</summary>
    public string? PoolEndpoint { get; init; }

    /// <summary>
    /// Whether this machine accepts configuration a control plane offers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off unless it is here and true, and it is NOT an offerable key</b> —
    /// which is the whole safety argument for offered configuration rather than
    /// a detail of it. A control plane that could set this would be granting
    /// itself the ability to configure the machine.
    /// </para>
    /// <para>
    /// <b>And it has no environment variable either.</b> A variable would be a
    /// second way to turn it on, and one that a container image or a systemd
    /// unit could carry without anybody reading it. Turning this on means
    /// opening the file, which is the deliberate amount of friction for a
    /// decision to let something else configure your machine.
    /// </para>
    /// </remarks>
    public bool? AcceptOffered { get; init; }

    // WHERE `accept-unattended` WAS, and why it is not. It was for a pool
    // member with nobody watching - and a pool member has no file to set it in:
    // DockerPoolAdapter creates members with no binds, so nothing from the host
    // filesystem reaches one. It was a switch for a machine that could not
    // reach it, and the only acceptance path is now a person typing
    // `gg config accept`.
    //
    // The GUARD against a redirect applying unattended stays - see
    // OfferableKeys.NeedsAPerson. It defends a door that is currently locked,
    // which is the right state for a door that may be unlocked later.

    /// <summary>
    /// Whether a person may put a credential on this machine over the channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The door the note above describes, with a key that fits it.</b>
    /// <c>accept-unattended</c> died because a member could not reach the file
    /// it lived in. This one is not reached by an operator either - a member
    /// WRITES it at its own first start, on the authority of the single-use
    /// nonce its tenant minted to create it. A laptop opts in by opening its
    /// file; a member is opted in by whoever made it, and the written line is
    /// what makes that auditable from inside the container.
    /// </para>
    /// <para>
    /// <b>Off unless it is here and true, no environment variable, and not
    /// offerable</b> - <see cref="AcceptOffered"/>'s three rules, and the last
    /// one matters more here than it does there. A control plane that could set
    /// this would be granting itself the ability to put a SECRET on the
    /// machine, which is the one thing Article VIII says it never holds.
    /// </para>
    /// <para>
    /// <b>It gates the wiring rather than a check.</b> A machine that has not
    /// set this hands the runner nowhere to keep a credential, so the dispatch
    /// arm refuses for want of a port rather than for want of a permission -
    /// which is the same shape as a runner handed no private key being
    /// unreachable, and it is why there is no second place to get it wrong.
    /// </para>
    /// </remarks>
    public bool? AcceptConfigured { get; init; }

    /// <summary>Relay addresses for the runner and console peer connection.</summary>
    public string? StunServers { get; init; }

    /// <summary>
    /// Where the estate's working copy is, as a path on this machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The verbs took the current directory and nothing could name one.</b>
    /// That answers for a verb typed inside the tree and not at all for the
    /// console, which is launched from wherever somebody happened to be.
    /// </para>
    /// <para>
    /// <b>No default, and a fallback is not one.</b> The verbs still fall back
    /// to the current directory so nothing that works today stops, but there is
    /// no path that is right for every machine - a default here would be a
    /// directory gg wrote an estate into because nobody said otherwise.
    /// </para>
    /// <para>
    /// <b>Not offerable, unlike everything a fleet needs.</b> It names a
    /// directory on this machine, and a control plane able to move where a
    /// person's drafts live could move where their next edit lands. It keeps a
    /// variable, unlike <see cref="AcceptOffered"/>, because a container or a
    /// CI job has a legitimate reason to set one.
    /// </para>
    /// </remarks>
    public string? Airspace { get; init; }

    /// <summary>Which allowance this machine spends from.</summary>
    /// <remarks>
    /// <para>
    /// <b>A name, and never the thing itself</b> — this file's rule one level
    /// up, applied to a subscription instead of a credential. What crosses to a
    /// control plane is the string typed here, not an account id, an email, an
    /// organisation uuid or a token, and nothing in this project can resolve
    /// one.
    /// </para>
    /// <para>
    /// <b>Two machines may carry the same name, and that is the feature.</b> A
    /// laptop and a server signed in to one subscription are one allowance;
    /// saying so is how their spending is added up rather than counted twice.
    /// Nothing checks the claim, and nothing could — a control plane that could
    /// tell two subscriptions apart would be holding something about them.
    /// </para>
    /// <para>
    /// <b>Unset means this machine reports nothing.</b> Not zero, and not a
    /// name derived from the hostname: an allowance nobody named is one nobody
    /// has agreed to lend, and inventing an identifier for it would put a
    /// machine into a fleet's accounting by default.
    /// </para>
    /// </remarks>
    public string? Allowance { get; init; }

    /// <summary>The ceilings that allowance is measured against.</summary>
    /// <remarks>
    /// <para>
    /// <b>Here because nowhere else has them.</b> Nothing on the machine
    /// records a subscription's limits — a percentage exists only where the
    /// provider shows it — so the denominator is typed by the person who knows
    /// which plan they are on. <c>session=88000,week=2400000</c>.
    /// </para>
    /// <para>
    /// <b>Text, parsed by its reader</b>, like every other member: a value from
    /// the file and a value from the variable reach one parser and one refusal.
    /// <c>AllowanceLedger</c> drops an entry it cannot read rather than
    /// refusing the reading, because the token counts are right whether or not
    /// the ceiling is.
    /// </para>
    /// </remarks>
    public string? AllowanceLimits { get; init; }

    /// <summary>
    /// Whether this console draws the fleet's allowances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off unless it is here and true, and it is NOT an offerable key</b> —
    /// <see cref="AcceptOffered"/>'s rule, and its argument transfers: a
    /// control plane that could set this would be deciding what a person's own
    /// console shows them about everybody else.
    /// </para>
    /// <para>
    /// <b>And it has no environment variable either</b>, for the same reason
    /// one member along. A variable is a second way to turn it on, and one a
    /// container image or a systemd unit could carry without anybody reading
    /// it. Turning this on means opening the file, which is the deliberate
    /// amount of friction for showing one person what everybody else is
    /// spending.
    /// </para>
    /// <para>
    /// <b>It is not a permission.</b> The control plane answers with this
    /// person's own allowances unless it knows them to be an administrator, so
    /// this decides whether a pane is DRAWN and nothing about what it could
    /// hold. A local flag that granted visibility would be a client-side
    /// authorization check, which anybody could edit their way past.
    /// </para>
    /// </remarks>
    public bool? FleetAllowances { get; init; }

    /// <summary>The version of the last offer accepted here.</summary>
    /// <remarks>
    /// <para>
    /// <b>A record, not a setting</b> — which is why it has no row in
    /// <see cref="Members"/> and no variable. Nobody chooses it; accepting an
    /// offer writes it. It is here because a control plane's one standing
    /// document would otherwise be new every time anybody looked, and a
    /// re-offer could not be told from a withdraw-and-reissue.
    /// </para>
    /// <para>
    /// <b>And not offerable</b>, for <see cref="AcceptOffered"/>'s reason one
    /// step along: a control plane able to write "your machine accepted this"
    /// could stop a machine being offered something it never took.
    /// </para>
    /// <para>
    /// <b>This file is also the whole of the attribution.</b> Nothing is
    /// reported back — a machine that told its control plane what it accepted
    /// would turn a carried offer into a tracked instruction, and would need a
    /// write route where there is deliberately only a read. The file lives
    /// under one person's <c>XDG_CONFIG_HOME</c>, so whoever owns it is whoever
    /// accepted.
    /// </para>
    /// </remarks>
    public string? AcceptedOffer { get; init; }

    /// <summary>One member: the variable it answers, its key, and how to read and set it.</summary>
    public sealed record Member
    {
        /// <summary>The environment variable this member stands in for.</summary>
        public required string Variable { get; init; }

        /// <summary>How it is spelled in the file.</summary>
        public required string Key { get; init; }

        /// <summary>Its value as text, or null when unset.</summary>
        public required Func<Configuration, string?> Get { get; init; }

        /// <summary>The configuration with this member set.</summary>
        public required Func<Configuration, string, Configuration> With { get; init; }
    }

    /// <summary>
    /// Every setting the file can carry, and the variable each one answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One table, read by four things</b>: the blank rule below, the
    /// resolution in <see cref="Settings"/>, <c>gg config set</c>, and the page
    /// that says whether a value can be in the file at all. A member added
    /// without a row here is a member the file silently cannot carry and nothing
    /// holds to the blank rule — which is the one blank nobody checked.
    /// </para>
    /// <para>
    /// <b>Reading and writing are both here</b> rather than the second being
    /// derived: an accessor pair that disagreed would set one member and read
    /// another, and no test that only round-trips a whole document would notice.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Member> Members { get; } =
    [
        new() { Variable = "GG_CONTROL_PLANE", Key = "control-plane",
                Get = c => c.ControlPlane, With = (c, v) => c with { ControlPlane = v } },
        new() { Variable = "EDITOR", Key = "editor",
                Get = c => c.Editor, With = (c, v) => c with { Editor = v } },
        new() { Variable = "GG_TAKE_COMMAND", Key = "take-command",
                Get = c => c.TakeCommand, With = (c, v) => c with { TakeCommand = v } },
        new() { Variable = "GG_INTENT_HOSTS", Key = "intent-hosts",
                Get = c => c.IntentHosts, With = (c, v) => c with { IntentHosts = v } },
        new() { Variable = "GG_INTENT_READERS", Key = "intent-readers",
                Get = c => c.IntentReaders, With = (c, v) => c with { IntentReaders = v } },
        new() { Variable = "GG_VCS_HOSTS", Key = "vcs-hosts",
                Get = c => c.VcsHosts, With = (c, v) => c with { VcsHosts = v } },
        new() { Variable = "GG_DESTINATION_APIS", Key = "destination-apis",
                Get = c => c.DestinationApis, With = (c, v) => c with { DestinationApis = v } },
        new() { Variable = "GG_TRACKER_APIS", Key = "tracker-apis",
                Get = c => c.TrackerApis, With = (c, v) => c with { TrackerApis = v } },
        new() { Variable = "GG_EXECUTOR_BINARY", Key = "executor-binary",
                Get = c => c.ExecutorBinary, With = (c, v) => c with { ExecutorBinary = v } },
        new() { Variable = "GG_RUNNER_LABELS", Key = "runner-labels",
                Get = c => c.RunnerLabels, With = (c, v) => c with { RunnerLabels = v } },

        // THE ONE NUMBER, rendered to text here so the resolution has one shape
        // to work in. Its variable carries text like every other, and the file
        // holds a number because that is what a person editing would write.
        new() { Variable = "GG_RUNNER_HOLD_SECONDS", Key = "runner-hold-seconds",
                Get = c => c.RunnerHoldSeconds?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                With = (c, v) => c with
                {
                    RunnerHoldSeconds = int.TryParse(
                        v, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                        ? seconds
                        : throw new ArgumentOutOfRangeException(
                            nameof(v), v, "'runner-hold-seconds' is a whole number of seconds."),
                } },

        new() { Variable = "GG_POOL_ENDPOINT", Key = "pool-endpoint",
                Get = c => c.PoolEndpoint, With = (c, v) => c with { PoolEndpoint = v } },
        new() { Variable = "GG_STUN_SERVERS", Key = "stun-servers",
                Get = c => c.StunServers, With = (c, v) => c with { StunServers = v } },
        new() { Variable = "GG_AIRSPACE", Key = "airspace",
                Get = c => c.Airspace, With = (c, v) => c with { Airspace = v } },
        new() { Variable = "GG_ALLOWANCE", Key = "allowance",
                Get = c => c.Allowance, With = (c, v) => c with { Allowance = v } },
        new() { Variable = "GG_ALLOWANCE_LIMITS", Key = "allowance-limits",
                Get = c => c.AllowanceLimits, With = (c, v) => c with { AllowanceLimits = v } },
    ];

    /// <summary>Why this configuration cannot be used, or null when it can.</summary>
    /// <remarks>
    /// A sentence naming the offending value, never a bool —
    /// <c>Envelope.Validate</c>'s rule, and this document is hand-edited more
    /// often than an envelope is.
    /// </remarks>
    public static string? Validate(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var member in Members)
        {
            var key = member.Key;

            if (member.Get(configuration) is { } value && string.IsNullOrWhiteSpace(value))
            {
                return $"'{key}' is set to a blank value. Blank is not the same as unset: "
                     + "whoever typed it wrote a value, and the reader would see nothing "
                     + $"there and use the default instead. Remove the line to mean unset.";
            }
        }

        // THE RECORD IS HELD TO THE SAME BLANK RULE, and it needs its own clause
        // because it has no row in Members - it is written by accepting an offer
        // rather than chosen. A blank here would read as "nothing accepted"
        // while somebody had written a value, which is the one blank the loop
        // above exists to refuse.
        if (configuration.AcceptedOffer is { } accepted && string.IsNullOrWhiteSpace(accepted))
        {
            return "'accepted-offer' is set to a blank value. It names the offer this "
                 + "machine took, so blank would read as 'nothing accepted' while somebody "
                 + "had written a value. Remove the line to mean nothing was accepted.";
        }

        if (configuration.RunnerHoldSeconds is { } hold && hold < 1)
        {
            return $"'runner-hold-seconds' is {hold}, and a hold is at least one second. "
                 + "A claim that waits no time comes back empty as fast as the machine can "
                 + "ask, which is a busy loop against the control plane rather than a "
                 + "configuration.";
        }

        foreach (var (key, address) in ((string Key, string? Value)[])
                 [("control-plane", configuration.ControlPlane),
                  ("pool-endpoint", configuration.PoolEndpoint)])
        {
            // THE SCHEME IS CHECKED, NOT JUST ABSOLUTENESS, and the difference
            // is the mistake this is for. `localhost:5199` parses as absolute -
            // `localhost:` is read as the scheme - so a person who copied the
            // default without its `http://` would get a document that validated
            // and a machine that could not reach anything. The default in the
            // help text is `http://localhost:5199`, which makes dropping the
            // scheme the likeliest thing anybody types wrong here.
            if (address is { Length: > 0 }
             && (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                return $"'{key}' is '{address}', which is not an http or https address. It "
                     + "is where a request is sent, so a value like this fails at the first "
                     + "call with a message about the call rather than about the file.";
            }
        }

        return null;
    }
}
