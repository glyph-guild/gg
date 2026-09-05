using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Which links a runner's declared hosts actually serve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract 0.86.0 named this and left it open:</b> <i>"FlightRepos.From
/// reads only a URI's AbsolutePath and the registry matches on that path alone,
/// so a link at any host resolves to whichever registered entry shares its
/// path."</i> A link at <c>anywhere.invalid/acme/widgets</c> resolves to the
/// registered <c>acme/widgets</c> and a flight opens against it.
/// </para>
/// <para>
/// <b>It is not an oversight — it is a consequence of a boundary.</b> The
/// registry deliberately holds no host: <i>"which host a runner sends a
/// customer's credential to is a runner-side resolution; a policy store that
/// contained hosts would make credential destination a policy edit."</i> The
/// control plane cannot check a host because it must not know one.
/// </para>
/// <para>
/// <b>So it is checked HERE, where the mapping already lives.</b>
/// <c>GG_VCS_HOSTS</c> is the runner's own declaration of which provider key
/// reaches which host — the exact thing the guard says belongs runner-side —
/// and reading it is what lets a link from somewhere nobody declared be refused
/// before any source is fetched.
/// </para>
/// </remarks>
public class ALinkComesFromAServedHostTests
{
    private static IReadOnlyList<HostDeclaration> Declared(string raw) =>
        [.. HostDeclaration.ParseAll(raw, "GG_VCS_HOSTS")];

    private static HostDeclaration One(string raw) => Declared(raw)[0];

    [Test]
    public async Task A_bare_host_serves_every_link_at_that_host()
    {
        var declaration = One("forge=forge.invalid");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets"))).IsTrue();
        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/anything/else"))).IsTrue();
    }

    [Test]
    public async Task A_host_at_a_different_name_is_not_served()
    {
        // THE HOLE, as one line. This is the comparison nothing was making.
        var declaration = One("forge=forge.invalid");

        await Assert.That(declaration.Serves(new Uri("https://anywhere.invalid/acme/widgets")))
            .IsFalse()
            .Because("a link that merely shares a path with something registered is not a link "
                   + "to it, and acting on one fetches somebody else's repository.");
    }

    [Test]
    public async Task A_prefix_scopes_the_organisation_above_the_path()
    {
        // The spelling GG_VCS_HOSTS already uses, and the reason it exists: a
        // forge that puts an organisation above the repository path serves two
        // different tenants' repositories from one host.
        var declaration = One("forge=forge.invalid/acme");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets/x"))).IsTrue();
        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/other/widgets/x")))
            .IsFalse();
    }

    [Test]
    public async Task A_host_is_compared_without_regard_to_case()
    {
        // Hosts are case-insensitive, and so is the organisation segment on
        // every forge this has met. A REFUSAL that fired on a capital letter
        // would be one nobody could act on, and being too strict is the
        // dangerous direction for a refusal.
        var declaration = One("forge=Forge.Invalid/Acme");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets"))).IsTrue();
    }

    [Test]
    public async Task The_suffixes_a_declaration_carries_do_not_change_what_it_serves()
    {
        // !pathscoped and !nopr describe how a forge SPELLS things. They are
        // stripped before the host is compared, or a deployment that declared
        // one would silently serve nothing.
        var declaration = One("forge=forge.invalid/acme!pathscoped");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets"))).IsTrue();
    }

    // ---- what the runner does with it ----

    [Test]
    public async Task A_link_from_a_host_the_provider_serves_is_not_refused()
    {
        await Assert.That(HostDeclaration.Unserved(
                "forge", "https://forge.invalid/acme/widgets/pull/1",
                Declared("forge=forge.invalid/acme")))
            .IsNull();
    }

    [Test]
    public async Task A_link_from_a_host_the_provider_does_not_serve_is_refused_by_name()
    {
        var why = HostDeclaration.Unserved(
            "forge", "https://anywhere.invalid/acme/widgets/pull/1",
            Declared("forge=forge.invalid/acme"));

        await Assert.That(why).IsNotNull();
        await Assert.That(why!).Contains("anywhere.invalid");
        await Assert.That(why).Contains("forge")
            .Because("naming the host and the provider is the difference between a diagnosis and "
                   + "a refusal somebody has to go and reproduce.");
    }

    [Test]
    public async Task A_provider_this_runner_declares_nothing_for_is_not_refused_here()
    {
        // ABSENCE IS NOT A MISMATCH. A provider with no declaration is a
        // capability gap the vcs adapter reports in its own words; refusing it
        // here would be a second, worse copy of that message - and would ground
        // flights on a runner that had simply not been told about a forge.
        await Assert.That(HostDeclaration.Unserved(
                "another", "https://forge.invalid/acme/widgets",
                Declared("forge=forge.invalid/acme")))
            .IsNull();
    }

    [Test]
    public async Task A_flight_with_no_link_is_never_refused_for_where_it_came_from()
    {
        // A ticket names a provider and an id and no link; a sentence names
        // nothing at all. Neither has a host to check.
        await Assert.That(HostDeclaration.Unserved("forge", null, Declared("forge=forge.invalid")))
            .IsNull();
        await Assert.That(HostDeclaration.Unserved("forge", "not a uri", Declared("forge=forge.invalid")))
            .IsNull();
    }

    // ---- and which tracker can read a link ----

    [Test]
    public async Task The_provider_serving_a_link_is_the_one_that_declared_its_host()
    {
        // This is what gives a uri work-item flight a tool: the reader is keyed
        // on a provider, and a link carries none until this answers.
        await Assert.That(HostDeclaration.ProviderFor(
                "https://forge.invalid/acme/_workitems/edit/18120",
                Declared("forge=forge.invalid/acme,other=other.invalid")))
            .IsEqualTo("forge");
    }

    [Test]
    public async Task Two_declarations_serving_one_link_answer_nothing()
    {
        // Article XI. Two providers answering to one link is a configuration
        // question for a person, and picking one would be the guess this whole
        // design exists to avoid.
        await Assert.That(HostDeclaration.ProviderFor(
                "https://forge.invalid/acme/widgets",
                Declared("one=forge.invalid,two=forge.invalid/acme")))
            .IsNull();
    }

    [Test]
    public async Task A_link_nobody_declared_a_host_for_answers_nothing()
    {
        await Assert.That(HostDeclaration.ProviderFor(
                "https://anywhere.invalid/x/y", Declared("forge=forge.invalid")))
            .IsNull();
    }
}

/// <summary>
/// What the runner does with a link, before it fetches anything.
/// </summary>
/// <remarks>
/// <b>Both halves are only true at the loop.</b> A comparison nothing calls is a
/// comparison that does nothing — the shape this repository keeps finding — so
/// these assert the refusal reaching a FACT and the tool reaching the agent,
/// rather than the functions that decide them.
/// </remarks>
public class ALinkIsCheckedBeforeAnythingIsFetchedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Records every request it is handed, and never fails.</summary>
    private sealed class Recording : Gg.Runner.Execution.IExecutorPort
    {
        internal List<Gg.Runner.Execution.ExecutorRequest> Seen { get; } = [];

        public Gg.Runner.Execution.ExecutorCapabilities Capabilities =>
            Gg.Runner.Execution.ClaudeCodeExecutor.Capabilities;

        public Task<Gg.Runner.Execution.ExecutorRun> ExecuteAsync(
            Gg.Runner.Execution.ExecutorRequest request,
            CancellationToken cancellationToken = default)
        {
            Seen.Add(request);
            return Task.FromResult(Gg.Runner.Execution.ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.Zero,
                movesUsed: [Gg.Contracts.LoopMoves.Read]));
        }
    }

    private static Gg.Contracts.LeaseGranted ALease(GitFixture fixture, string uri) => new()
    {
        LeaseId = "lease-host",
        Generation = 1,
        FlightId = "flight-host",
        FlightNumber = Gg.Contracts.Description.FlightRef.Format(29),
        Repos =
        [
            new Gg.Contracts.LeaseRepoRef
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            },
        ],
        Credentials = [],
        ClassificationCeiling = Gg.Contracts.Classifications.Internal,
        ClassificationRules = Gg.Contracts.ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        IntentUri = uri,
        Loop = new Gg.Contracts.LeaseLoop
        {
            LoopId = "implement",
            Executor = Gg.Contracts.ExecutorRungs.Frontier,
            Moves = [Gg.Contracts.LoopMoves.Read],
            WallClockSeconds = 600,
            OnExhaustion = Gg.Contracts.ExhaustionPolicies.HandoffToAgent,
        },
    };

    private static async Task<(string Reason, Recording Executor)> RunAsync(string uri, string hosts)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture, uri)));
        var observer = new RecordingObserver();
        var executor = new Recording();

        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await new RunnerLoop(protocol, clock,
                (span, token) => { token.ThrowIfCancellationRequested(); clock.Advance(span); return Task.CompletedTask; },
                observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
                executor: executor,
                hosts: [.. HostDeclaration.ParseAll(hosts, "GG_VCS_HOSTS")])
            { HoldFor = TimeSpan.FromSeconds(3) }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        // THE RELEASE, not a loop outcome. A refusal that comes before anything
        // is fetched comes before there is a loop to have an outcome, so the
        // reason rides the release - the same channel the credential refusal
        // one statement above it already uses.
        return (string.Join("\n", protocol.Serialized), executor);
    }

    [Test]
    public async Task A_link_from_an_unserved_host_is_refused_and_nothing_is_fetched()
    {
        // THE HOLE'S CONSEQUENCE, stopped at the only layer that can see it. The
        // control plane created this flight because the path matched something
        // registered; the host says it is a different repository entirely.
        var (reason, executor) = await RunAsync(
            "https://anywhere.invalid/acme/widgets/pull/1",
            $"{LocalVcsAdapter.ProviderKey}=forge.invalid/acme");

        await Assert.That(reason).Contains("anywhere.invalid");
        await Assert.That(executor.Seen.Any(r => r.WorkingDirectory.Contains("widgets", StringComparison.Ordinal)))
            .IsFalse()
            .Because("the refusal has to come before anything is fetched, or it is a report "
                   + "about source that is already on this disk.");
    }

    [Test]
    public async Task A_link_shaped_work_item_is_given_the_provider_that_serves_it()
    {
        // THE OTHER HALF. A reader is keyed on a provider and a link carries
        // none, so without this a flight opened from a work-item URL reaches an
        // agent with nothing that can read it - which is the gap the last live
        // flight ended on.
        var (_, executor) = await RunAsync(
            "https://forge.invalid/acme/_workitems/edit/18120",
            $"{LocalVcsAdapter.ProviderKey}=forge.invalid/acme");

        await Assert.That(executor.Seen.Any(r => r.IntentProvider == LocalVcsAdapter.ProviderKey))
            .IsTrue()
            .Because("the provider is what ReaderFor matches, and a link that names none reaches "
                   + "the agent with no tracker tool at all.");
    }

    // ---- the local provider, whose declaration is not a host at all ----

    private static IReadOnlyList<HostDeclaration> Declaring(string raw) =>
        [.. HostDeclaration.ParseAll(raw, "GG_VCS_HOSTS")];

    [Test]
    public async Task A_filesystem_root_is_not_refused_for_not_being_a_hostname()
    {
        // THE CATEGORY ERROR. GG_VCS_HOSTS carries two different things under
        // one field: for a forge, Host is a HOSTNAME and this check is exactly
        // right; for the `local` provider, HttpsGitVcsAdapter passes the very
        // same value to LocalVcsAdapter as its FILESYSTEM ROOT. Comparing a
        // link's host to a path can never match, so every link-shaped flight
        // against a local repository was refused before anything was fetched -
        // and the refusal told an operator to declare a host they had already
        // declared.
        //
        // Found by slice twenty-five's walk, which needs a bare repository on
        // disk and a link-shaped intent; the control plane will only attach a
        // repository from an http(s) uri, so the two rules had no overlap.
        // resume-two-hosts-e2e.sh is configured the same way and cannot have
        // worked since this check landed.
        var refusal = HostDeclaration.Unserved(
            "local",
            "https://forge.example/acme/invoices/tree/main",
            Declaring("local=/srv/repos"));

        await Assert.That(refusal).IsNull()
            .Because("a filesystem root is not a claim about a host, so there is nothing here "
                   + "to disagree with the link about. It said: " + refusal);
    }

    [Test]
    public async Task A_forge_at_the_wrong_host_is_still_refused()
    {
        // THE LIVENESS TWIN, and it is the whole reason the carve-out is keyed
        // on the provider rather than on "does this look like a path". A rule
        // that exempted anything path-shaped would exempt a forge declared with
        // an organisation prefix, which is the case this check was built for.
        var refusal = HostDeclaration.Unserved(
            "forge",
            "https://anywhere.invalid/acme/widgets/tree/main",
            Declaring("forge=forge.invalid/acme"));

        await Assert.That(refusal).IsNotNull()
            .Because("a link that merely shares a path with something registered is not a link "
                   + "to it, and that is untouched.");
        await Assert.That(refusal!).Contains("anywhere.invalid");
    }

    [Test]
    public async Task The_local_carve_out_is_the_provider_the_adapter_actually_reads_as_a_root()
    {
        // NAMED FROM THE ADAPTER, never spelled twice. The carve-out is only
        // correct for the provider whose declaration HttpsGitVcsAdapter hands
        // to LocalVcsAdapter as a root; if that key ever changes, this test is
        // what makes the exemption move with it rather than quietly cover the
        // wrong provider.
        var refusal = HostDeclaration.Unserved(
            LocalVcsAdapter.ProviderKey,
            "https://forge.example/acme/invoices/tree/main",
            Declaring($"{LocalVcsAdapter.ProviderKey}=/srv/repos"));

        await Assert.That(refusal).IsNull();
    }
}
