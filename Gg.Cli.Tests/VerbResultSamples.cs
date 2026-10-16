using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A representative instance of every <see cref="VerbResult"/> kind, so its text
/// rendering can be read by a voice check.
/// </summary>
/// <remarks>
/// Keyed by the record's type. A kind added to <see cref="VerbResult"/> without
/// a sample here fails <c>Every_verb_result_kind_has_a_sample</c>, the way a new
/// reason kind fails the contract's sentence check. Each kind carries the
/// branches a person is most likely to read: the empty answer and a populated
/// one, and where the wording forks on a state, each side of the fork.
/// </remarks>
internal static class VerbResultSamples
{
    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 30, 0, TimeSpan.Zero);

    private static readonly Reason Waiting = Reason.For(ReasonKinds.Uncharted, ["staging"]);

    private static FlightIntent Ticket => new() { Kind = FlightIntentKinds.Ticket, Provider = "ado", Id = "4217" };

    private static FlightSummary Summary(IReadOnlyList<FactEnvelope>? facts = null) => new()
    {
        FlightId = "019ff8aa-0000-7000-8000-000000000042",
        FlightNumber = "GG-42",
        Name = "Fix the login redirect",
        Intent = Ticket,
        CreatedAt = At,
        RunnerProtocolVersion = 7,
        FactVocabularyVersion = "v12",
        ConstitutionVersion = "c3",
        EnvelopeVersion = "v18",
        Attempts = 1,
        Facts = facts ?? [],
        Waiting = Waiting,
        State = FlightStates.Open,
        Model = "opus",
    };

    private static EnvironmentIdentity Identity => new()
    {
        HostFingerprint = "sha256:0123456789abcdef0123456789abcdef",
        ImageDigest = null,
        Locks = [],
        Tools = [new ToolVersion { Name = "dotnet", Version = "10.0.102" }],
        Provenance = "probed",
    };

    private static FactEnvelope Fact(string kind) => new()
    {
        IdempotencyKey = $"GG-42:{kind}:1",
        Kind = kind,
        Digest = "sha256:abc",
        ObservedAt = At,
    };

    private static IReadOnlyList<FactEnvelope> RichFacts =>
    [
        Fact("environment") with { Environment = Identity },
        Fact("change") with
        {
            Change = new ChangeManifest
            {
                BaseCommit = "0123456789abcdef",
                HeadCommit = "fedcba9876543210",
                Resolution = ChangeResolution.Files,
                DiffBasis = "merge-base",
                Paths = [],
                Directories = [],
                Languages = [],
                FilesChanged = 3,
                LinesAdded = 20,
                LinesRemoved = 4,
                PathsWithheld = 2,
            },
        },
        Fact("loop") with
        {
            Loop = new LoopOutcome
            {
                LoopId = "implement",
                Outcome = LoopOutcomes.Blocked,
                Reason = "The approver must choose a database.",
                Executor = "claude",
                Attempts = 4,
                DurationMs = 61_000,
                MovesUsed = ["edit", "search"],
            },
        },
    ];

    private static NominationSummary Nomination(string? ending, string mode) => new()
    {
        NominationId = Guid.Parse("019ff8aa-0000-7000-8000-0000000000a1"),
        Nominator = "watch:stale-branches",
        Subject = "ado#4217",
        Reason = "The branch has had no commits for 30 days.",
        Version = "v2",
        WorkKind = "implement",
        Mode = mode,
        State = ending is null ? "standing" : "ended",
        Ending = ending,
        Because = ending is null ? null : "A person declined it.",
        FlightNumber = ending == NominationEndings.Opened ? "GG-43" : null,
        ItineraryNumber = "ITN-7",
        MadeAt = At,
        IntentKey = "ado#4217",
        For = "acme:alice",
        ForDisplay = "Alice",
    };

    private static Observation Looked(string state) => new()
    {
        State = state,
        Because = "The control plane recorded it.",
        Outcome = "approved",
        WaitedSeconds = 1.5,
        BoundSeconds = 10,
        Polls = 3,
    };

    private static AllowanceWindow Window(double? reported = null, long? limit = null) => new()
    {
        Kind = "session",
        InputTokens = 1_000,
        OutputTokens = 2_000,
        CacheReadTokens = 90_000,
        CacheWriteTokens = 500,
        Reported = reported,
        ResetsAt = At.AddHours(3),
        ReportedAt = reported is null ? null : At,
        Since = At.AddHours(-2),
        Limit = limit,
    };

    private static Envelope AnEnvelope => new()
    {
        Context = new ContextBinding { Scope = "implement", Constitution = "c3" },
        Obligations = [],
        Loops = [],
        Destinations = [],
    };

    private static EnvironmentStrategyState Strategy => new()
    {
        Name = "payments-pool",
        Version = "v3",
        AppliedAt = At,
        Strategy = new EnvironmentStrategy
        {
            Kind = "pool",
            Environment = "staging",
            Inventory = new StrategyInventory { Pool = "payments-pool", Size = 4, Warm = 2 },
            PullPoint = "registry.example/payments",
            Image = "registry.example/payments:v3",
            Bounds = new StrategyBounds { PoolMax = 6 },
        },
    };

    private static RunnerSummary Runner(string ownership, bool reserved = false) => new()
    {
        RunnerId = "laptop-7",
        Label = "laptop-7",
        State = "online",
        LastHeartbeatAt = At,
        Ownership = ownership,
        Owner = ownership == RunnerOwnerships.Claimed ? "acme:alice" : "",
        Reserved = reserved,
        Labels = [new AdvertisedLabel { Name = "environment=staging", Disposition = "advertised" }],
    };

    private static DoctorCheck[] Checks =>
    [
        new() { Name = "session", Passed = true, Detail = "signed in as acme:alice", Blocking = true, Fixable = true },
        new() { Name = "control-plane", Passed = false, Detail = "not reachable", Blocking = true, Fixable = true, Fix = "check GG_CONTROL_PLANE" },
        new() { Name = "runner", Passed = false, Detail = "no runner credential", Blocking = false, Fixable = false },
        new() { Name = "airspace", Passed = true, Detail = "GG_AIRSPACE is unset", Blocking = false, Fixable = true, Discloses = true },
    ];

    private static PendingGate Gate => new()
    {
        FlightNumber = "GG-42",
        ObligationId = "root.approver",
        Approver = "acme:alice",
        Branch = "gg/GG-42",
        Commit = "0123456789abcdef",
        ManifestHash = "sha256:abc",
        Because = "A person must approve a change to the payments service.",
        AwaitingSince = At,
        Attempt = 1,
        Nomination = new GateNomination { Reason = "The branch is stale.", WorkKind = "implement" },
    };

    private static NameDeclared Declared(string? flight = null, string? whose = null, string? by = null) => new()
    {
        Name = "payments",
        Role = "work-kind",
        Parent = "root",
        Flight = flight,
        Awaiting = flight is null ? null : "acme:alice",
        Widens = flight is null ? null : "airspace.names",
        For = whose,
        WroteTo = null,
        DeclaredBy = by,
    };

    /// <summary>Every kind, each with one or more samples.</summary>
    public static IReadOnlyDictionary<Type, VerbResult[]> All { get; } = new Dictionary<Type, VerbResult[]>
    {
        [typeof(VerbResult.Flights)] =
        [
            new VerbResult.Flights(new FlightList { Flights = [] }),
            new VerbResult.Flights(new FlightList { Flights = [Summary()], Next = "cursor-2" }),
        ],
        [typeof(VerbResult.Flight)] = [new VerbResult.Flight(Summary()), new VerbResult.Flight(Summary(RichFacts))],
        [typeof(VerbResult.Launched)] =
        [
            new VerbResult.Launched(new FlightLaunched { FlightId = "019ff8aa-0000-7000-8000-000000000042" }),
            new VerbResult.Launched(new FlightLaunched { FlightId = "019ff8aa-0000-7000-8000-000000000042", FlightNumber = "GG-42" }),
        ],
        [typeof(VerbResult.Log)] =
        [
            new VerbResult.Log(new FlightLog { FlightId = "019ff8aa-42", FlightNumber = "GG-42", Entries = [] }),
            new VerbResult.Log(new FlightLog
            {
                FlightId = "019ff8aa-42",
                FlightNumber = "GG-42",
                Entries = [new FlightLogEntry { At = At, Kind = "leased", Detail = "laptop-7 took it" }],
            }),
        ],
        [typeof(VerbResult.Facts)] =
        [
            new VerbResult.Facts(new FlightFacts { FlightNumber = "GG-42", Facts = [] }),
            new VerbResult.Facts(new FlightFacts
            {
                FlightNumber = "GG-42",
                Facts = RichFacts.Select(f => new RecordedFact { Fact = f, Disposition = "accepted", RecordedAt = At }).ToList(),
            }),
        ],
        [typeof(VerbResult.Story)] =
        [
            new VerbResult.Story(new FlightStory
            {
                FlightId = "019ff8aa-42",
                FlightNumber = "GG-42",
                Stage = FlightStages.Created,
                State = FlightStates.Open,
                Entries = [],
                Outstanding = [],
            }),
            new VerbResult.Story(
                new FlightStory
                {
                    FlightId = "019ff8aa-42",
                    FlightNumber = "GG-42",
                    WorkKind = "implement",
                    Stage = FlightStages.Leased,
                    State = FlightStates.Open,
                    Waiting = Waiting,
                    For = "acme:alice",
                    ForDisplay = "Alice",
                    HeldBy = new Actor { Kind = "runner", Name = "laptop-7" },
                    HeldUntil = At,
                    Entries = [new StoryEntry { At = At, Kind = StoryKinds.All.First(), Said = "Starting." }],
                    Outstanding = [],
                },
                new LegHold { Follows = "GG-41", Subject = "ado#4216" },
                "opus"),
        ],
        [typeof(VerbResult.Runners)] =
        [
            new VerbResult.Runners(new RunnerList { Runners = [] }),
            new VerbResult.Runners(new RunnerList
            {
                Runners =
                [
                    Runner(RunnerOwnerships.Tenant),
                    Runner(RunnerOwnerships.Claimed, reserved: true) with { RunnerId = "desk-2", Label = "desk-2" },
                ],
            }),
        ],
        [typeof(VerbResult.Chart)] =
        [
            new VerbResult.Chart(new EnvironmentChart { Environments = [] }),
            new VerbResult.Chart(new EnvironmentChart
            {
                Environments = [new EnvironmentCharted { Name = "staging", Disposition = "charted", ChartedBy = "acme:alice", ChartedAt = At, Meaning = "environment=staging" }],
            }),
        ],
        [typeof(VerbResult.Pools)] =
        [
            new VerbResult.Pools(new PoolLedger { Pools = [] }),
            new VerbResult.Pools(new PoolLedger
            {
                Pools = [new PoolStatus { Pool = "payments-pool", Action = "warm", Outcome = "ok", MeasuredAt = At, Diagnosis = "Two members are warm." }],
            }),
        ],
        [typeof(VerbResult.RunnerFlights)] =
        [
            new VerbResult.RunnerFlights("laptop-7", new RunnerFlightList { Flights = [] }),
            new VerbResult.RunnerFlights("laptop-7", new RunnerFlightList
            {
                Flights = [new RunnerFlight { FlightId = "019ff8aa-42", Number = 42, Kind = "implement", State = "landed", ClaimedAt = At, EndedAt = At }],
            }),
        ],
        [typeof(VerbResult.StrategyBuild)] =
        [
            new VerbResult.StrategyBuild(new PoolAction
            {
                ActionId = Guid.Parse("019ff8aa-0000-7000-8000-0000000000b1"),
                Pool = "payments-pool",
                Action = "build",
                StrategyVersion = "v3",
                DecidedAt = At,
            }),
        ],
        [typeof(VerbResult.Watches)] =
        [
            new VerbResult.Watches(new WatchStandingList { Standings = [] }),
            new VerbResult.Watches(new WatchStandingList
            {
                Standings =
                [
                    new WatchStanding
                    {
                        Name = "stale-branches",
                        Version = "v2",
                        Window = "P1D",
                        Opened = 1,
                        Budgeted = 3,
                        Outcome = "swept",
                        Nominated = 1,
                        LastHeardAt = At,
                        QuietSince = At,
                        Diagnosis = "No runner advertises the sweep.",
                        Account = "acme:alice",
                    },
                ],
                Next = "cursor-2",
            }),
        ],
        [typeof(VerbResult.Strategies)] =
        [
            new VerbResult.Strategies(new StrategyList { Strategies = [] }),
            new VerbResult.Strategies(new StrategyList { Strategies = [Strategy] }),
        ],
        [typeof(VerbResult.Invited)] =
        [
            new VerbResult.Invited(new InvitationIssued { InvitationUrl = "https://gg.example/join/abc", ExpiresAt = At }),
        ],
        [typeof(VerbResult.AirspaceTopology)] =
        [
            new VerbResult.AirspaceTopology(new EnvelopeTopology
            {
                Names = [new TopologyName { Name = "payments", Role = "work-kind", Parent = "root", DeclaredBy = "acme:alice", DeclaredAt = At }],
            }),
        ],
        [typeof(VerbResult.AirspaceRepositories)] =
        [
            new VerbResult.AirspaceRepositories(new RegisteredRepositories { Repositories = [] }, []),
            new VerbResult.AirspaceRepositories(
                new RegisteredRepositories
                {
                    Repositories =
                    [
                        new RepositoryRegistered
                        {
                            Name = "widgets",
                            Provider = "example-forge",
                            Id = "1234",
                            Path = "acme/widgets",
                            Credential = "required",
                            RegisteredBy = "acme:alice",
                            RegisteredAt = At,
                        },
                    ],
                },
                []),
        ],
        [typeof(VerbResult.AirspacePulled)] =
        [
            new VerbResult.AirspacePulled(new TreeWritten { Written = [], Removed = [], Unrepresentable = [] }),
            new VerbResult.AirspacePulled(new TreeWritten { Written = ["root.yaml"], Removed = ["old.yaml"], Unrepresentable = ["a/b"] }),
        ],
        [typeof(VerbResult.AirspaceApplied)] =
        [
            new VerbResult.AirspaceApplied(new EstateApplied { Applied = [], Retiring = [], Declared = [] }),
            new VerbResult.AirspaceApplied(new EstateApplied
            {
                Applied =
                [
                    new AppliedDocument { Name = "payments", Path = "payments.yaml", Version = "v4", Changed = true },
                    new AppliedDocument { Name = "root", Path = "root.yaml", Version = "v18", Changed = false },
                    new AppliedDocument { Name = "review", Path = "review.yaml", Version = "v2", Changed = false, Flight = "GG-44", Awaiting = "acme:alice", Widens = "obligations" },
                ],
                Retiring = ["legacy"],
                Declared = [Declared(), Declared(flight: "GG-45")],
                Refused = new ApplyRefusal { Name = "broken", Path = "broken.yaml", Diagnosis = "The file is not valid YAML.", NotTried = ["later.yaml"] },
            }),
        ],
        [typeof(VerbResult.EnvironmentCharted)] =
        [
            new VerbResult.EnvironmentCharted(new Gg.Client.EnvironmentChartEntry { Name = "staging" }),
        ],
        [typeof(VerbResult.NameDeclared)] =
        [
            new VerbResult.NameDeclared(Declared()),
            new VerbResult.NameDeclared(Declared(flight: "GG-45")),
            new VerbResult.NameDeclared(Declared(whose: "acme:alice")),
            new VerbResult.NameDeclared(Declared(whose: "acme:alice") with { WroteTo = "payments.yaml" }),
            new VerbResult.NameDeclared(Declared(by: "acme:bob")),
        ],
        [typeof(VerbResult.RepositoryAdded)] =
        [
            new VerbResult.RepositoryAdded(new Gg.Client.RepositoryAdded { Name = "widgets", Provider = "example-forge", Id = "1234", Path = "acme/widgets", RegisteredBy = "acme:alice" }),
            new VerbResult.RepositoryAdded(new Gg.Client.RepositoryAdded { Name = "widgets", Provider = "example-forge", Id = "1234", Path = "acme/widgets", Flight = "GG-46", Awaiting = "acme:alice", Widens = "airspace.repositories" }),
        ],
        [typeof(VerbResult.AirspaceDocuments)] =
        [
            new VerbResult.AirspaceDocuments(new AirspaceEstate { Documents = [], Strategies = [] }),
            new VerbResult.AirspaceDocuments(new AirspaceEstate { Documents = [], Strategies = [Strategy] }),
        ],
        [typeof(VerbResult.RulesInForce)] = [new VerbResult.RulesInForce("implement", AnEnvelope)],
        [typeof(VerbResult.NamedEnvelopeShown)] =
        [
            new VerbResult.NamedEnvelopeShown(new NamedEnvelopeState { Name = "payments", Role = "work-kind", Version = "v4", UpdatedAt = At, UpdatedBy = "acme:alice" }),
            new VerbResult.NamedEnvelopeShown(new NamedEnvelopeState
            {
                Name = "payments-fix",
                Role = "work-kind",
                Version = "v1",
                UpdatedAt = At,
                UpdatedBy = "acme:alice",
                Extension = new EnvelopeExtension { Extends = "payments" },
            }),
        ],
        [typeof(VerbResult.StrategyShown)] = [new VerbResult.StrategyShown(Strategy)],
        [typeof(VerbResult.NameRetired)] =
        [
            new VerbResult.NameRetired(new Gg.Client.NameRetired { Name = "legacy", Version = "v19" }),
            new VerbResult.NameRetired(new Gg.Client.NameRetired { Name = "legacy", Version = "v19", Flight = "GG-47", Awaiting = "acme:alice" }),
        ],
        [typeof(VerbResult.AirspaceDiffed)] =
        [
            new VerbResult.AirspaceDiffed(new EstateDiff { Changes = [], Retiring = [], Unreadable = [] }),
            new VerbResult.AirspaceDiffed(new EstateDiff
            {
                Changes = [new DocumentChange { Name = "payments", Path = "payments.yaml", Direction = "widens", Field = "obligations" }],
                Retiring = ["legacy"],
                Unreadable = [new UnreadableDocument { Path = "broken.yaml", Diagnosis = "The file is not valid YAML." }],
            }),
        ],
        [typeof(VerbResult.Plan)] =
        [
            new VerbResult.Plan(new Checklist { EnvelopeVersion = "v18", RequiredLabels = [], Items = [] }),
            new VerbResult.Plan(new Checklist
            {
                EnvelopeVersion = "v18",
                FlightNumber = "GG-42",
                Environment = "staging",
                RequiredLabels = ["environment=staging"],
                Items =
                [
                    new ChecklistItem { Requirement = "environment=staging", Verification = "label", Satisfier = ChecklistSatisfiers.MatchingRunner, Disposition = "met" },
                    new ChecklistItem { Requirement = "gpu=true", Verification = "label", Satisfier = ChecklistSatisfiers.Nobody, Disposition = "unmet", WhenUnmet = Reason.For(ReasonKinds.NoRunnerAdvertises, ["gpu=true"]) },
                ],
            }),
        ],
        [typeof(VerbResult.ItineraryChecked)] =
        [
            new VerbResult.ItineraryChecked(new ItineraryCheck
            {
                Planner = "plan",
                Refused = "The plan names no legs.",
                Legs =
                [
                    new LegCheck
                    {
                        Subject = "ado#4217",
                        WorkKind = "implement",
                        Verdict = "would open",
                        Reason = "Every obligation is satisfied.",
                        Obligations = [],
                        Gates = [new LegGate { ObligationId = "root.approver", Approver = "acme:alice" }],
                        PassedOver = [],
                    },
                ],
            }),
        ],
        [typeof(VerbResult.ItineraryProposed)] =
        [
            new VerbResult.ItineraryProposed(new Gg.Contracts.ItineraryProposed
            {
                Itinerary = "ITN-7",
                Pass = "pass-1",
                Gates = [new LegGate { ObligationId = "root.approver", Approver = "acme:alice" }, new LegGate { ObligationId = "root.review" }],
            }),
        ],
        [typeof(VerbResult.RunnerLabels)] =
        [
            new VerbResult.RunnerLabels(new RunnerList { Runners = [] }),
            new VerbResult.RunnerLabels(new RunnerList { Runners = [Runner(RunnerOwnerships.Open)] }),
        ],
        [typeof(VerbResult.Diagnosis)] = [new VerbResult.Diagnosis(new DoctorReport { Checks = Checks })],
        [typeof(VerbResult.Credentials)] =
        [
            new VerbResult.Credentials(new CredentialList { Credentials = [] }, []),
            new VerbResult.Credentials(
                new CredentialList
                {
                    Credentials =
                    [
                        new CredentialSummary
                        {
                            CredentialId = "cred-1",
                            For = "acme/widgets",
                            AddedAt = At,
                            Reference = new CredentialReference { Kind = "token", Locator = "store:acme-widgets", Identity = "acme-bot", Scopes = ["repo"] },
                        },
                    ],
                },
                []),
        ],
        [typeof(VerbResult.CredentialAdded)] =
        [
            new VerbResult.CredentialAdded(new CredentialRegistered
            {
                CredentialId = "cred-1",
                AddedAt = At,
                Reference = new CredentialReference { Kind = "token", Locator = "store:acme-widgets", Identity = "acme-bot", Scopes = ["repo"] },
            }),
        ],
        [typeof(VerbResult.CredentialRemoved)] =
        [
            new VerbResult.CredentialRemoved(new Gg.Contracts.CredentialRemoved
            {
                CredentialId = "cred-1",
                Reference = new CredentialReference { Kind = "token", Locator = "store:acme-widgets", Identity = "acme-bot", Scopes = ["repo"] },
            }),
        ],
        [typeof(VerbResult.Keys)] =
        [
            new VerbResult.Keys(new PrincipalKeyList { Keys = [] }),
            new VerbResult.Keys(new PrincipalKeyList
            {
                Keys = [new PrincipalKeySummary { KeyId = "key-1", Principal = "acme:alice", PublicKey = "MFkw", Fingerprint = "SHA256:abc", RegisteredAt = At, RetiredAt = At }],
            }),
        ],
        [typeof(VerbResult.KeyCreated)] =
        [
            new VerbResult.KeyCreated(new VerbResult.KeyMinted("~/.config/gg/key.pem", "MFkw", "Registered as SHA256:abc, so a credential can be sealed to this person.")),
        ],
        [typeof(VerbResult.CredentialTrusted)] =
        [
            new VerbResult.CredentialTrusted(new VerbResult.MachineTrusted("store:acme-widgets", 2, AlreadyWas: false)),
            new VerbResult.CredentialTrusted(new VerbResult.MachineTrusted("store:acme-widgets", 2, AlreadyWas: true)),
        ],
        [typeof(VerbResult.CredentialAudienceFound)] =
        [
            new VerbResult.CredentialAudienceFound(new VerbResult.AudienceList("store:acme-widgets", [])),
            new VerbResult.CredentialAudienceFound(new VerbResult.AudienceList(
                "store:acme-widgets",
                [new CredentialAudienceRow("laptop-7", "laptop-7", "store:acme-widgets", true, false, true, null)])),
        ],
        [typeof(VerbResult.RunnerRepinned)] =
        [
            new VerbResult.RunnerRepinned(new Gg.Client.RunnerRepinned { RunnerId = "laptop-7", Forgotten = true }),
            new VerbResult.RunnerRepinned(new Gg.Client.RunnerRepinned { RunnerId = "laptop-7", Forgotten = false }),
        ],
        [typeof(VerbResult.RunnerRetired)] =
        [
            new VerbResult.RunnerRetired(new Gg.Contracts.RunnerRetired { RunnerId = "laptop-7", RetiredAt = At }),
        ],
        [typeof(VerbResult.TenantNamed)] = [new VerbResult.TenantNamed("Acme")],
        [typeof(VerbResult.EnrollmentMinted)] =
        [
            new VerbResult.EnrollmentMinted(new EnrollmentTokenMinted
            {
                TokenId = "tok-1",
                Token = "ggenroll_abc",
                Profile = "build-box",
                Uses = 3,
                ExpiresAt = At,
                Ownership = RunnerOwnerships.Tenant,
            }),
        ],
        [typeof(VerbResult.EnrollmentTokens)] =
        [
            new VerbResult.EnrollmentTokens(new EnrollmentTokenList { Tokens = [] }),
            new VerbResult.EnrollmentTokens(new EnrollmentTokenList
            {
                Tokens = [new EnrollmentTokenSummary { TokenId = "tok-1", Profile = "build-box", UsesLeft = 2, ExpiresAt = At, Ownership = RunnerOwnerships.Tenant, MintedBy = "acme:alice", RevokedAt = At }],
            }),
        ],
        [typeof(VerbResult.EnrollmentRevoked)] =
        [
            new VerbResult.EnrollmentRevoked(new EnrollmentTokenSummary { TokenId = "tok-1", Profile = "build-box", UsesLeft = 2, ExpiresAt = At, Ownership = RunnerOwnerships.Tenant }),
        ],
        [typeof(VerbResult.RunnerOwned)] =
        [
            new VerbResult.RunnerOwned(new RunnerOwnership { RunnerId = "laptop-7", Ownership = RunnerOwnerships.Tenant }),
            new VerbResult.RunnerOwned(new RunnerOwnership { RunnerId = "laptop-7", Ownership = RunnerOwnerships.Open }),
            new VerbResult.RunnerOwned(new RunnerOwnership { RunnerId = "laptop-7", Ownership = RunnerOwnerships.Claimed, Owner = "acme:alice" }),
            new VerbResult.RunnerOwned(new RunnerOwnership { RunnerId = "laptop-7", Ownership = RunnerOwnerships.Claimed, Owner = "acme:alice", Reserved = true }),
            new VerbResult.RunnerOwned(new RunnerOwnership { RunnerId = "laptop-7", Ownership = "borrowed" }),
        ],
        [typeof(VerbResult.RunnerReservation)] =
        [
            new VerbResult.RunnerReservation(new RunnerReserved { RunnerId = "laptop-7", ReservedTo = "acme:alice" }),
            new VerbResult.RunnerReservation(new RunnerReserved { RunnerId = "laptop-7" }),
        ],
        [typeof(VerbResult.Bundle)] =
        [
            new VerbResult.Bundle(new DiagnosticsBundle
            {
                TakenAt = At,
                Completeness = "partial",
                CompletenessDetail = "no flight was named, so no flight log is included",
                Binary = "0.88.40",
                Protocol = 7,
                FactVocabulary = "v12",
                Environment = Identity,
                Checks = Checks,
                Degradations = [new BundleDegradation { Name = "clock", Detail = "the clock is 3s fast", Remedy = "sync the clock", Blocking = false }],
            }),
        ],
        [typeof(VerbResult.EnvelopeShown)] =
        [
            new VerbResult.EnvelopeShown(new EnvelopeState { Version = "v18", Envelope = AnEnvelope, UpdatedAt = At, UpdatedBy = "acme:alice" }),
        ],
        [typeof(VerbResult.EnvelopeApplied)] =
        [
            new VerbResult.EnvelopeApplied(new Gg.Contracts.EnvelopeApplied { Version = "v19", AppliedAt = At, Changed = true }, ["The comment on line 3 was dropped."]),
            new VerbResult.EnvelopeApplied(new Gg.Contracts.EnvelopeApplied { Version = "v18", AppliedAt = At, Changed = false }, []),
            new VerbResult.EnvelopeApplied(new Gg.Contracts.EnvelopeApplied { Version = "v18", AppliedAt = At, Changed = false, Flight = "GG-48", Widens = "obligations", Awaiting = "acme:alice" }, []),
        ],
        [typeof(VerbResult.EnvelopeValidated)] =
        [
            new VerbResult.EnvelopeValidated(new EnvelopeValidation { Role = "root", Valid = false, Diagnosis = "The envelope declares no obligations." }),
            new VerbResult.EnvelopeValidated(new EnvelopeValidation { Role = "root", Valid = true, Notes = ["The comment on line 3 will be dropped."], Canonical = "context:\n  scope: root\n" }),
        ],
        [typeof(VerbResult.ConfigShown)] =
        [
            new VerbResult.ConfigShown(new ConfigurationView
            {
                Path = "/nonexistent/gg/config.yaml",
                Settings = [new EnvironmentSetting { Name = "GG_CONTROL_PLANE", Value = "https://cp.example", Why = "the control plane", Source = "environment", Shadowed = "https://old.example" }],
                AcceptsOffered = true,
                AcceptsConfigured = false,
            }),
            new VerbResult.ConfigShown(new ConfigurationView
            {
                Path = "/nonexistent/gg/config.yaml",
                Settings = [new EnvironmentSetting { Name = "EDITOR", Value = "vim", Why = "the editor", Source = "file" }],
                AcceptsOffered = false,
                AcceptsConfigured = true,
            }),
        ],
        [typeof(VerbResult.Allowance)] =
        [
            new VerbResult.Allowance(new AllowanceReading { Allowance = "team-max", MeasuredAt = At, Windows = [Window(), Window(reported: 0.4), Window(limit: 88_000)] }),
            new VerbResult.Allowance(new AllowanceReading { Allowance = "team-max", MeasuredAt = At.AddDays(2), Windows = [Window(reported: 0.4)] }),
        ],
        [typeof(VerbResult.Allowances)] =
        [
            new VerbResult.Allowances(new AllowanceList { Allowances = [] }),
            new VerbResult.Allowances(new AllowanceList
            {
                Allowances =
                [
                    new AllowanceSummary { Name = "team-max", MeasuredAt = At, Windows = [Window(), Window(reported: 0.4), Window(limit: 88_000)], Runners = ["laptop-7"] },
                    new AllowanceSummary { Name = "team-pro", MeasuredAt = At.AddDays(2), Windows = [Window(reported: 0.4)], Runners = ["laptop-7", "desk-2"] },
                ],
            }),
        ],
        [typeof(VerbResult.ConfigValidated)] =
        [
            new VerbResult.ConfigValidated(new ConfigurationValidation { Valid = false, Diagnosis = "Line 2: `editor` is not a setting." }),
            new VerbResult.ConfigValidated(new ConfigurationValidation { Valid = true, Canonical = "editor: vim\n" }),
            new VerbResult.ConfigValidated(new ConfigurationValidation { Valid = true }),
        ],
        [typeof(VerbResult.ConfigOffered)] =
        [
            new VerbResult.ConfigOffered(new OfferedView { Path = "/nonexistent/gg/config.yaml", Changes = [] }),
            new VerbResult.ConfigOffered(new OfferedView
            {
                Path = "/nonexistent/gg/config.yaml",
                Version = "v3",
                OfferedAt = At,
                NeedsAPerson = true,
                Changes =
                [
                    new OfferedChange { Key = "control-plane", Offered = "https://cp.example", Current = "https://old.example", Changes = true },
                    new OfferedChange { Key = "editor", Offered = "vim", Changes = true },
                    new OfferedChange { Key = "allowance", Offered = "team-max", Current = "team-max" },
                ],
            }),
            new VerbResult.ConfigOffered(new OfferedView { Path = "/nonexistent/gg/config.yaml", Version = "v3", Accepted = true, AcceptsOffered = true, Changes = [new OfferedChange { Key = "editor", Offered = "vim", Changes = true }] }),
            new VerbResult.ConfigOffered(new OfferedView { Path = "/nonexistent/gg/config.yaml", Version = "v3", AlreadyAccepted = true, AcceptsOffered = true, Changes = [] }),
            new VerbResult.ConfigOffered(new OfferedView { Path = "/nonexistent/gg/config.yaml", Version = "v3", Refused = "It names a setting this gg does not know: `color`.", AcceptsOffered = true, Changes = [] }),
        ],
        [typeof(VerbResult.Why)] =
        [
            new VerbResult.Why(new FlightAttribution { FlightNumber = "GG-42", EnvelopeVersion = "v18", Obligations = [] }),
            new VerbResult.Why(new FlightAttribution
            {
                FlightNumber = "GG-42",
                EnvelopeVersion = "v18",
                Halt = "The runner stopped the flight.",
                Obligations =
                [
                    new ObligationAttribution { ObligationId = "root.approver", Attachment = Attachments.Attached, Because = "A person must approve.", Outcome = "pending" },
                ],
            }),
        ],
        [typeof(VerbResult.Identity)] =
        [
            new VerbResult.Identity(new WhoAmI
            {
                PrincipalId = "acme:alice",
                PrincipalDisplay = "Alice",
                TenantId = "019ff8aa-1111",
                TenantDisplay = "Acme",
                ExpiresAt = At,
                IsAdmin = true,
                Subject = "alice@acme.example",
            }),
        ],
        [typeof(VerbResult.Gates)] =
        [
            new VerbResult.Gates(new GateList { Gates = [] }),
            new VerbResult.Gates(new GateList { Gates = [Gate, Gate with { Commit = null, Condition = "context.scope == 'payments'" }] }),
        ],
        [typeof(VerbResult.Board)] =
        [
            new VerbResult.Board(new BoardPage { Nominations = [], IncludedEnded = false }),
            new VerbResult.Board(new BoardPage { Nominations = [], IncludedEnded = true }),
            new VerbResult.Board(new BoardPage
            {
                Nominations =
                [
                    Nomination(null, DestinationOpening.Gated),
                    Nomination(null, DestinationOpening.Auto),
                    Nomination(NominationEndings.Opened, DestinationOpening.Auto),
                ],
                IncludedEnded = true,
                Next = "cursor-2",
            }),
            new VerbResult.Board(new BoardPage { Nominations = [Nomination(null, DestinationOpening.Gated)], IncludedEnded = false }),
        ],
        [typeof(VerbResult.Itineraries)] =
        [
            new VerbResult.Itineraries(new BoardPage { Nominations = [], IncludedEnded = true }),
            new VerbResult.Itineraries(new BoardPage
            {
                Nominations = [Nomination(null, DestinationOpening.Gated), Nomination(NominationEndings.Opened, DestinationOpening.Auto)],
                IncludedEnded = true,
            }),
        ],
        [typeof(VerbResult.NominationDecided)] =
        [
            new VerbResult.NominationDecided(new NominationDecisionReport { Observation = Looked(ObservationStates.Decided) }),
            new VerbResult.NominationDecided(new NominationDecisionReport
            {
                Nomination = Nomination(NominationEndings.Opened, DestinationOpening.Gated) with { FlightNumber = null },
                Observation = Looked(ObservationStates.Decided),
            }),
        ],
        [typeof(VerbResult.Decided)] =
        [
            new VerbResult.Decided(new DecisionReport { Observation = Looked(ObservationStates.Decided) }),
            new VerbResult.Decided(new DecisionReport { Observation = Looked(ObservationStates.Refused) }),
            new VerbResult.Decided(new DecisionReport
            {
                Observation = Looked("pending"),
                Decision = new DecisionRecorded { FlightNumber = "GG-42", ObligationId = "root.approver", Outcome = "approved", DecidedBy = "acme:alice", DecidedAt = At },
            }),
        ],
        [typeof(VerbResult.Taken)] =
        [
            new VerbResult.Taken(
                new TakeSeed
                {
                    Revision = 1,
                    FlightNumber = "GG-42",
                    FlightId = "019ff8aa-42",
                    Measurements = new TakeMeasurements
                    {
                        FilesEdited = [],
                        FilesReadNotEdited = [],
                        Searches = [],
                        Errors = [],
                        UndeclaredMovesUsed = [],
                        Attempts = 1,
                        StopReason = "completed",
                    },
                    AccountState = "absent",
                    TranscriptState = "absent",
                },
                ["The takeover lasts until 10:30."]),
        ],
    };
}
