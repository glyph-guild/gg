using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Landing against a real remote, including the case where nothing lands.
/// </summary>
/// <remarks>
/// <para>
/// <b>Excluded from CI by name</b>, like the agent tests, and for the same
/// reason: these need a credential and the network, and they refuse loudly
/// rather than passing when unconfigured.
/// </para>
/// <para>
/// <b>The negative case is the important one, and it needs the remote.</b>
/// "Nothing was pushed" asserted by watching a fake is a statement about the
/// fake. The assertion that matters is <i>this branch does not exist on the
/// remote</i>, made by asking the remote - and it is only worth anything next to
/// the positive case, because a runner that could never push would satisfy the
/// negative one perfectly.
/// </para>
/// <para>
/// Every test cleans up after itself: a branch left behind makes the next run's
/// negative assertion pass for the wrong reason, and an open proposal left
/// behind makes the idempotency assertion pass without proving anything. The
/// cleanup is <i>part of the test</i> rather than hygiene.
/// </para>
/// </remarks>
[Category("RealRemote")]
public class AgainstRealRemoteTests
{
    private const string HostVariable = "GG_FIXTURE_HOST";
    private const string ApiVariable = "GG_FIXTURE_API";
    private const string SlugVariable = "GG_FIXTURE_SLUG";
    private const string SecretVariable = "GG_FIXTURE_SECRET";

    /// <summary>Who the credential belongs to, so authorship is asserted rather than read back.</summary>
    private const string IdentityVariable = "GG_FIXTURE_IDENTITY";

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{variable} is not set. These land real branches on a real remote and assert against "
              + "it; skipping them would leave the claim this whole step rests on unverified - that "
              + "a flight which was not admitted leaves nothing behind.");

    private static string Slug => Required(SlugVariable);

    private static string Secret => Required(SecretVariable);

    private static HttpClient Api()
    {
        var http = new HttpClient { BaseAddress = new Uri(Required(ApiVariable)) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Secret);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("gg-tests", "1"));

        return http;
    }

    private static HttpsDestinationAdapter Adapter(HttpClient api) =>
        new("fixture", Required(HostVariable), api);

    /// <summary>A branch name no other run will collide with.</summary>
    /// <remarks>
    /// The flight number is what a real branch carries. These invent one,
    /// because two runs of this suite must not both be trying to create
    /// <c>gg/GG-1</c>.
    /// </remarks>
    private static string Number() => "GG-T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>A clone of the fixture with one file edited, as an agent would leave it.</summary>
    private static async Task<string> WorkedTreeAsync(string content)
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-remote-" + Guid.NewGuid().ToString("N")[..8]);
        var url = $"https://{Required(HostVariable)}/{Slug}.git";

        Directory.CreateDirectory(root);

        // The same shape the workspace materializes: shallow, one ref, and no
        // remote configured - so a push has to be told where to go rather than
        // inheriting an origin somebody set up.
        await GitInvocation.Plain("init", "--initial-branch=main").RunAsync(root, CancellationToken.None);
        await GitInvocation.Fetch(url, "refs/heads/main", Secret).RunAsync(root, CancellationToken.None);
        await GitInvocation.Plain("checkout", "FETCH_HEAD").RunAsync(root, CancellationToken.None);

        await File.WriteAllTextAsync(Path.Combine(root, "GG-LANDED.md"), content);

        return root;
    }

    private static LandingRequest Request(string tree, string branch) => new()
    {
        WorkingDirectory = tree,
        Slug = Slug,
        Branch = branch,
        BaseRef = "main",
        Title = "gg fixture landing",
        Secret = Secret,
    };

    /// <summary>Whether the remote holds this branch. Asked OF THE REMOTE.</summary>
    private static async Task<bool> ExistsAsync(HttpClient api, string branch)
    {
        using var response = await api.GetAsync($"repos/{Slug}/branches/{branch}");

        return response.IsSuccessStatusCode;
    }

    private static async Task<string?> HeadAsync(HttpClient api, string branch)
    {
        using var response = await api.GetAsync($"repos/{Slug}/branches/{branch}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("commit").GetProperty("sha").GetString();
    }

    /// <summary>The open proposals for a branch, as numbers.</summary>
    private static async Task<List<int>> ProposalsAsync(HttpClient api, string branch)
    {
        var owner = Slug.Split('/')[0];
        using var response = await api.GetAsync(
            $"repos/{Slug}/pulls?state=open&head={Uri.EscapeDataString($"{owner}:{branch}")}");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return [.. document.RootElement.EnumerateArray().Select(p => p.GetProperty("number").GetInt32())];
    }

    /// <summary>Closes anything this test opened and deletes the branch it made.</summary>
    private static async Task CleanAsync(HttpClient api, string branch)
    {
        var owner = Slug.Split('/')[0];
        using var listed = await api.GetAsync(
            $"repos/{Slug}/pulls?state=open&head={Uri.EscapeDataString($"{owner}:{branch}")}");

        if (listed.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
            foreach (var open in document.RootElement.EnumerateArray())
            {
                using var closing = await api.PatchAsJsonAsync(
                    $"repos/{Slug}/pulls/{open.GetProperty("number").GetInt32()}",
                    new Dictionary<string, string> { ["state"] = "closed" });
            }
        }

        using var deleted = await api.DeleteAsync($"repos/{Slug}/git/refs/heads/{branch}");
    }

    // ---- the positive half ----

    [Test]
    public async Task An_admitted_flight_lands_a_branch_and_a_proposal()
    {
        // Half of what this step claims, and the half without which the negative
        // assertion below proves nothing at all: a runner incapable of pushing
        // would satisfy "no branch exists" perfectly.
        using var api = Api();
        var branch = DestinationBranch.For(Number());
        var tree = await WorkedTreeAsync("landed by a governed flight\n");

        try
        {
            var outcome = await Adapter(api).LandAsync(Request(tree, branch));

            var landed = outcome as LandingOutcome.Landed;
            await Assert.That(landed).IsNotNull()
                .Because("the outcome was " + outcome);

            await Assert.That(await ExistsAsync(api, branch)).IsTrue()
                .Because("asserted against the remote, not against what the adapter returned.");
            await Assert.That((await ProposalsAsync(api, branch)).Count).IsEqualTo(1);
            await Assert.That(landed!.Uri).Contains(Slug)
                .Because("the reference recorded in destination.landed has to be one a person can open.");
            await Assert.That(landed.Number).IsGreaterThan(0);
        }
        finally
        {
            await CleanAsync(api, branch);
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task The_landed_branch_carries_the_flight_number()
    {
        // The branch is the only durable link between a proposal and the flight
        // that produced it. A name nobody can trace is a branch nobody will ever
        // delete - and this is asserted on the REMOTE's copy of the name.
        using var api = Api();
        var number = Number();
        var branch = DestinationBranch.For(number);
        var tree = await WorkedTreeAsync("traceable\n");

        try
        {
            await Adapter(api).LandAsync(Request(tree, branch));

            using var response = await api.GetAsync($"repos/{Slug}/branches/{branch}");
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            await Assert.That(document.RootElement.GetProperty("name").GetString()).Contains(number);
        }
        finally
        {
            await CleanAsync(api, branch);
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task The_proposal_is_authored_by_the_developer_and_not_by_the_platform()
    {
        // Article XII, and it comes for free from WHERE the write happens. The
        // runner pushes with the credential the developer registered, so the
        // proposal is theirs. The platform's own application holds no permission
        // to push or to propose - and adding either would make every existing
        // installation re-approve, which is a real cost paid for nothing, because
        // it does not need one.
        using var api = Api();
        var branch = DestinationBranch.For(Number());
        var tree = await WorkedTreeAsync("authored by a person\n");

        try
        {
            var landed = (LandingOutcome.Landed)await Adapter(api).LandAsync(Request(tree, branch));

            using var response = await api.GetAsync($"repos/{Slug}/pulls/{landed.Number}");
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var author = document.RootElement.GetProperty("user");

            await Assert.That(author.GetProperty("type").GetString()).IsEqualTo("User")
                .Because("a proposal from a machine account is one nobody is accountable for.");

            // Named, not read back. An assertion compared against whatever it
            // just fetched would pass for a proposal opened by anybody at all.
            await Assert.That(author.GetProperty("login").GetString())
                .IsEqualTo(Required(IdentityVariable))
                .Because("it is whoever registered the credential, which is the whole of the "
                       + "attribution story: the platform did not write this, a person's credential "
                       + "did.");
        }
        finally
        {
            await CleanAsync(api, branch);
            Directory.Delete(tree, recursive: true);
        }
    }

    // ---- through the loop, both halves, against the same remote ----

    private static readonly DateTimeOffset T0 = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// An agent that edits the tree, standing in for the real one.
    /// </summary>
    /// <remarks>
    /// These tests are about the destination, not the agent - but they need a
    /// tree with a change in it, because <b>a landing carries commits</b> and a
    /// flight whose agent did nothing has nothing to push. That is not a
    /// simulation shortcut: it is the same reason a real clean tree produces a
    /// refusal rather than an empty branch.
    /// </remarks>
    private sealed class WorkingAgent : Execution.IExecutorPort
    {
        public Execution.ExecutorCapabilities Capabilities => new()
        {
            Rung = "fixture-agent",
            EnforcesMoves = false,
            ReportsAttempts = true,
            ReportsTokens = false,
            ReportsDuration = true,
            ReportsMovesUsed = false,
            AttributesEditsToTools = false,
            Gaps =
            [
                new Execution.ExecutorGap
                {
                    Name = "does-one-thing",
                    Consequence = "It writes one file. These tests are about the destination, and a "
                                + "tree with a change in it is all the destination needs of an agent.",
                },
            ],
        };

        public async Task<Execution.ExecutorRun> ExecuteAsync(
            Execution.ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            await File.WriteAllTextAsync(
                Path.Combine(request.WorkingDirectory, "GG-LANDED.md"),
                "Written by a governed flight.\n", cancellationToken);

            return new Execution.ExecutorRun
            {
                LoopId = request.LoopId,
                Outcome = LoopOutcomes.Completed,
                Reason = "one file written",
                Attempts = 1,
                DurationMs = 1,
                MovesUsed = [LoopMoves.Edit],
            };
        }
    }

    /// <summary>A lease for the real fixture, with a credential the developer registered.</summary>
    private static LeaseGranted ALease(string number, params string[] scopes) => new()
    {
        LeaseId = "lease-fixture",
        Generation = 1,
        FlightId = Guid.NewGuid().ToString(),
        FlightNumber = number,
        Repos =
        [
            new LeaseRepoRef { Provider = "fixture", Slug = Slug, PinnedRef = "refs/heads/main" },
        ],
        Credentials =
        [
            new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = CredentialLocator.ForRepo(Slug),
                Identity = "gg-fixture",
                Scopes = scopes,
            },
        ],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        IntentUri = "https://example.invalid/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = "fixture-agent",
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 60,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    /// <summary>Runs one flight through the real loop against the real remote.</summary>
    /// <remarks>
    /// The whole point of going through the loop rather than calling the adapter:
    /// <b>the tree is held across the facts round trip</b>. The loop materializes
    /// it, ships facts, reads the decision off the response and only then pushes
    /// what it is still holding. Calling the adapter directly would skip the one
    /// mechanism this step introduced.
    /// </remarks>
    private static async Task<(RecordingObserver Observer, FakeProtocol Protocol)> FlyAsync(
        string number, DestinationAdmission? admission, params string[] scopes)
    {
        using var api = Api();
        using var trees = new ScratchTreeRoot();

        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol { Admission = admission };
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(number, scopes)));

        var observer = new RecordingObserver();
        var stopping = new CancellationTokenSource();
        var seen = 0;
        observer.OnEvent = _ =>
        {
            if (Interlocked.Increment(ref seen) >= 4)
            {
                stopping.Cancel();
            }
        };

        var workspace = trees.Workspace(
            [new HttpsGitVcsAdapter("fixture", Required(HostVariable), new VcsCapabilities
            {
                PullRequestHeadsFromBase = true,
                RefScheme = "refs/heads/<branch>",
            })]);

        using (stopping)
        {
            await new RunnerLoop(protocol, clock,
                    (span, token) => { token.ThrowIfCancellationRequested(); clock.Advance(span); return Task.CompletedTask; },
                    observer,
                    new ScriptedResolver
                    {
                        Secrets = { [CredentialLocator.ForRepo(Slug)] = Secret },
                    },
                    workspace,
                    executor: new WorkingAgent(),
                    destinations: [Adapter(api)])
                {
                    HoldFor = TimeSpan.FromSeconds(1),
                }
                .RunAsync("runner-fixture", ["linux"], stopping.Token);
        }

        return (observer, protocol);
    }

    [Test]
    public async Task An_admitted_flight_lands_through_the_loop_and_records_where()
    {
        // The sequence this step is: agent works, facts reported, admission
        // evaluated, THE FACTS RESPONSE CARRIES THE DECISION, the runner pushes.
        // The tree survived the round trip or this cannot pass.
        using var api = Api();
        var number = Number();
        var branch = DestinationBranch.For(number);

        try
        {
            var (observer, protocol) = await FlyAsync(number, new DestinationAdmission
            {
                DestinationId = "fixture-main",
                Branch = branch,
                BaseRef = "main",
                Slug = Slug,
                Reason = "the one obligation held",
            }, CredentialScopes.Read, CredentialScopes.Write);

            var landing = observer.Events.Single(e => e.StartsWith("landed:", StringComparison.Ordinal));
            await Assert.That(landing).IsEqualTo("landed:landed")
                .Because("the loop's own account of what it did comes first, so a failure here is "
                       + "readable rather than 'expected true'.");

            await Assert.That(await ExistsAsync(api, branch)).IsTrue()
                .Because("the loop pushed the tree it was still holding when the decision arrived.");

            var landed = protocol.ShippedFacts.SelectMany(b => b.Items)
                .Where(f => f.Kind == FactKinds.DestinationLanded)
                .ToList();

            await Assert.That(landed.Count).IsEqualTo(1)
                .Because("a landing nobody recorded is a branch nobody will ever delete.");
            await Assert.That(landed[0].Landed!.Branch).IsEqualTo(branch);
            await Assert.That(landed[0].Landed!.PullRequestNumber).IsGreaterThan(0);
            await Assert.That(landed[0].Landed!.PullRequestUri).Contains(Slug);
            await Assert.That(landed[0].Digest).IsNotNull()
                .Because("it is a fact like any other: digested before it left the machine.");
        }
        finally
        {
            await CleanAsync(api, branch);
        }
    }

    [Test]
    public async Task A_write_destination_against_a_read_only_credential_fails_at_the_credential()
    {
        // THE ASSERTION SLICE ONE COULD NOT VERIFY. The control plane admitted
        // this flight; the developer registered read. The envelope declared
        // permission and could not grant ability - so it fails here, and the
        // diagnosis names the credential rather than a status code.
        //
        // Nothing is attempted against the remote at all, which is why the
        // absence assertion below is about the design and not about luck.
        using var api = Api();
        var number = Number();
        var branch = DestinationBranch.For(number);

        var (observer, protocol) = await FlyAsync(number, new DestinationAdmission
        {
            DestinationId = "fixture-main",
            Branch = branch,
            BaseRef = "main",
            Slug = Slug,
            Reason = "the one obligation held",
        }, CredentialScopes.Read);

        var refusal = observer.Events.Single(e => e.StartsWith("landed:", StringComparison.Ordinal));

        await Assert.That(refusal).Contains("refused");
        await Assert.That(await ExistsAsync(api, branch)).IsFalse()
            .Because("an envelope cannot widen a credential, so there was never a push to refuse.");
        await Assert.That(protocol.ShippedFacts.SelectMany(b => b.Items)
                .Any(f => f.Kind == FactKinds.DestinationLanded)).IsFalse()
            .Because("nothing landed, so nothing says it did.");
    }

    [Test]
    public async Task A_flight_that_was_not_admitted_leaves_nothing_on_the_remote()
    {
        // THE CLAIM THIS STEP RESTS ON, through the loop. The tree is real, the credential
        // carries write, the adapter is configured and reachable - everything is
        // in place except the decision, and the decision is the thing.
        //
        // Asserted by asking the remote. "Nothing was pushed" observed on a
        // double is a statement about the double.
        using var api = Api();
        var number = Number();
        var branch = DestinationBranch.For(number);

        var (_, protocol) = await FlyAsync(number, admission: null,
            CredentialScopes.Read, CredentialScopes.Write);

        await Assert.That(await ExistsAsync(api, branch)).IsFalse()
            .Because("absent means no, and this asked the remote rather than trusting a call count.");
        await Assert.That((await ProposalsAsync(api, branch)).Count).IsEqualTo(0);
        await Assert.That(protocol.ShippedFacts.SelectMany(b => b.Items)
                .Any(f => f.Kind == FactKinds.DestinationLanded)).IsFalse();
    }

    // ---- an existing branch is refused, never overwritten ----

    [Test]
    public async Task An_existing_branch_is_refused_by_name_and_its_head_is_unchanged()
    {
        // The thing that would otherwise be got wrong: force-pushing when the
        // branch is there. The head sha is read BEFORE and AFTER, because
        // "refused" is a claim about the outcome and "not overwritten" is a
        // claim about somebody's work.
        using var api = Api();
        var branch = DestinationBranch.For(Number());
        var first = await WorkedTreeAsync("the first landing, which must survive\n");
        var second = await WorkedTreeAsync("a second flight, which must not overwrite it\n");

        try
        {
            var landed = await Adapter(api).LandAsync(Request(first, branch));
            await Assert.That(landed).IsTypeOf<LandingOutcome.Landed>();

            var before = await HeadAsync(api, branch);

            var refused = await Adapter(api).LandAsync(Request(second, branch));

            await Assert.That(refused).IsTypeOf<LandingOutcome.BranchExists>()
                .Because("it was " + refused);
            await Assert.That(((LandingOutcome.BranchExists)refused).Branch).IsEqualTo(branch)
                .Because("named, so a person knows which branch to look at.");
            await Assert.That(await HeadAsync(api, branch)).IsEqualTo(before)
                .Because("the branch on the remote is somebody's work and this did not touch it.");
        }
        finally
        {
            await CleanAsync(api, branch);
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }

    // ---- idempotency across the push/proposal seam ----

    [Test]
    public async Task Replaying_a_landing_finds_the_one_proposal_rather_than_opening_a_second()
    {
        // The seam: the push succeeds, the proposal fails, and the batch that
        // carried the admission is retried. Proven by REPLAYING it - the same
        // request twice against the same remote, from a second tree, which is
        // what a retried flight looks like.
        //
        // The second attempt is refused at the push (the branch is now there)
        // and that is fine: what must not happen is a second proposal, and the
        // first attempt's proposal must still be findable.
        using var api = Api();
        var branch = DestinationBranch.For(Number());
        var tree = await WorkedTreeAsync("landed once\n");

        try
        {
            var first = await Adapter(api).LandAsync(Request(tree, branch)) as LandingOutcome.Landed;
            await Assert.That(first).IsNotNull();

            // Replayed from a tree that has not pushed yet, so the proposal
            // query is what has to be idempotent rather than git.
            var replay = await WorkedTreeAsync("landed once\n");
            try
            {
                await Adapter(api).LandAsync(Request(replay, branch));
            }
            finally
            {
                Directory.Delete(replay, recursive: true);
            }

            await Assert.That((await ProposalsAsync(api, branch)).Count).IsEqualTo(1)
                .Because("creation is keyed on the branch, so a replay finds the proposal that "
                       + "exists. Two proposals for one flight is the failure this seam invites.");

            // And the one that exists is the one already recorded, so the
            // reference in destination.landed still resolves after a retry.
            await Assert.That(await ProposalsAsync(api, branch)).IsEquivalentTo((int[])[first!.Number])
                .Because("a retry that opened a second proposal would also have left the recorded "
                       + "reference pointing at the wrong one.");
        }
        finally
        {
            await CleanAsync(api, branch);
            Directory.Delete(tree, recursive: true);
        }
    }

    // ---- the refusal a developer has to be able to read ----

    [Test]
    public async Task A_credential_the_remote_will_not_write_with_is_refused_in_our_own_words()
    {
        // The report-back question, answered against a real remote: is this
        // diagnosable, or a 403 with somebody else's wording on it?
        //
        // The registered-scope refusal happens earlier and without a remote (see
        // DestinationWriteTests). This is the OTHER half: a developer registered
        // write, and the credential itself will not do it. A real push, really
        // rejected.
        using var api = Api();
        var branch = DestinationBranch.For(Number());
        var tree = await WorkedTreeAsync("this cannot be pushed\n");

        try
        {
            var outcome = await Adapter(api).LandAsync(new LandingRequest
            {
                WorkingDirectory = tree,
                Slug = Slug,
                Branch = branch,
                BaseRef = "main",
                Title = "gg fixture refusal",
                Secret = "not-a-credential-this-remote-will-accept",
            });

            var refused = outcome as LandingOutcome.CredentialRefused;
            await Assert.That(refused).IsNotNull()
                .Because("it was " + outcome);

            await Assert.That(refused!.Locator).IsEqualTo(CredentialLocator.ForRepo(Slug))
                .Because("naming the reference is what makes this actionable: the developer has more "
                       + "than one credential and needs to know which was too narrow.");
            await Assert.That(refused.Diagnosis).Contains("envelope")
                .Because("the diagnosis explains the two controls - a destination is permission to "
                       + "land somewhere and not the ability to.");
            await Assert.That(refused.Diagnosis).Contains("write scope");

            await Assert.That(await ExistsAsync(api, branch)).IsFalse()
                .Because("a refused push leaves nothing behind either.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }
}
