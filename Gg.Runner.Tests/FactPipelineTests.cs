using System.Reflection;
using System.Text.Json;
using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// The order is load-bearing, so the types carry it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The digest is computed BEFORE the filter</b>, or later analysis derives
/// from already-redacted material and every conclusion drawn from it is about a
/// document nobody produced. <b>The filter runs BEFORE egress</b>, because the
/// alternative is a system that sends everything and removes it at the far end,
/// which fails a security review for the obvious reason.
/// </para>
/// <para>
/// Both are asserted STRUCTURALLY rather than by a test that calls them in the
/// right order. A test like that passes while a second caller does it wrong;
/// these assertions are about what the code is able to express. Only
/// <c>Digest</c> produces a <see cref="DigestedFacts"/>, only <c>Filter</c>
/// consumes one, only <c>Filter</c> produces a <see cref="FilteredFacts"/>, and
/// only that is accepted for egress - so there is no way to write the other
/// order down.
/// </para>
/// <para>
/// Establishing this now, with one harmless fact in it, means step 7 adds a
/// fact rather than a pipeline. Getting it backwards means step 7 reorders a
/// path that already carries real content.
/// </para>
/// </remarks>
public class FactPipelineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private static EnvironmentIdentity AnEnvironment() => new()
    {
        HostFingerprint = new string('a', 64),
        ImageDigest = null,
        Locks = [],
        Tools = [new ToolVersion { Name = "git", Version = "2.50.1" }],
        Provenance = EnvironmentProvenance.Fresh,
    };

    private static GatheredFacts AGathering() =>
        new([new FactPayload.Environment(AnEnvironment())]);

    // ---- the structural half ----

    private static IEnumerable<MethodInfo> RunnerMethods() =>
        typeof(FactPipeline).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

    private static string Describe(MethodInfo method) => $"{method.DeclaringType?.Name}.{method.Name}";

    [Test]
    public async Task Only_the_digest_stage_produces_digested_facts()
    {
        var producers = RunnerMethods()
            .Where(m => m.ReturnType == typeof(DigestedFacts))
            .Select(Describe)
            .ToList();

        await Assert.That(producers).IsEquivalentTo((string[])[$"{nameof(FactPipeline)}.{nameof(FactPipeline.Digest)}"])
            .Because("a second way to make a DigestedFacts is a way to skip digesting. Found: "
                   + string.Join(", ", producers));
    }

    [Test]
    public async Task Only_the_filter_stage_consumes_digested_facts_and_only_it_produces_filtered_ones()
    {
        var consumers = RunnerMethods()
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(DigestedFacts)))
            .Select(Describe)
            .ToList();

        var producers = RunnerMethods()
            .Where(m => m.ReturnType == typeof(FilteredFacts))
            .Select(Describe)
            .ToList();

        await Assert.That(consumers).IsEquivalentTo((string[])[$"{nameof(FactPipeline)}.{nameof(FactPipeline.Filter)}"]);
        await Assert.That(producers).IsEquivalentTo((string[])[$"{nameof(FactPipeline)}.{nameof(FactPipeline.Filter)}"]);
    }

    [Test]
    public async Task Nothing_can_get_from_gathered_to_filtered_in_one_step()
    {
        // The bypass this whole arrangement exists to prevent: a convenience
        // method that gathers and ships, with the digest computed afterwards
        // from whatever survived.
        var bypasses = RunnerMethods()
            .Where(m => m.ReturnType == typeof(FilteredFacts))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(GatheredFacts)))
            .Select(Describe)
            .ToList();

        await Assert.That(bypasses).IsEmpty()
            .Because("Found: " + string.Join(", ", bypasses));
    }

    [Test]
    public async Task Egress_accepts_only_filtered_facts()
    {
        // The other end. Whatever ships them takes the type only Filter can
        // produce, so un-filtered facts cannot be handed to it.
        var ship = typeof(IRunnerProtocol).GetMethod(nameof(IRunnerProtocol.ShipFactsAsync))!;

        await Assert.That(ship.GetParameters().Select(p => p.ParameterType)).Contains(typeof(FilteredFacts));
        await Assert.That(ship.GetParameters().Any(p => p.ParameterType == typeof(GatheredFacts))).IsFalse();
        await Assert.That(ship.GetParameters().Any(p => p.ParameterType == typeof(DigestedFacts))).IsFalse();
    }

    [Test]
    public async Task The_structural_check_can_see_a_stage_at_all()
    {
        // Every assertion above is an absence over a reflection walk, and an
        // empty walk satisfies all of them.
        await Assert.That(RunnerMethods().Any(m => m.ReturnType == typeof(DigestedFacts))).IsTrue();
        await Assert.That(RunnerMethods().Any(m => m.ReturnType == typeof(FilteredFacts))).IsTrue();
    }

    // ---- the behavioural half ----

    [Test]
    public async Task The_digest_is_a_sha256_of_the_payload()
    {
        var digested = FactPipeline.Digest(AGathering(), "flight-1", T0);

        await Assert.That(digested.Items).HasCount(1);
        await Assert.That(FactEnvelope.Validate(digested.Items[0])).IsNull();
        await Assert.That(digested.Items[0].Digest).HasCount(64);
    }

    [Test]
    public async Task The_same_facts_digest_the_same_way_twice()
    {
        // A digest that moved between two identical gatherings would make
        // "replaying an identical batch changes nothing" false at the far end,
        // through no fault of the far end.
        var one = FactPipeline.Digest(AGathering(), "flight-1", T0);
        var other = FactPipeline.Digest(AGathering(), "flight-1", T0);

        await Assert.That(one.Items[0].Digest).IsEqualTo(other.Items[0].Digest);
        await Assert.That(one.Items[0].IdempotencyKey).IsEqualTo(other.Items[0].IdempotencyKey);
    }

    [Test]
    public async Task Different_facts_digest_differently()
    {
        // A digest that never moves is decoration.
        var one = FactPipeline.Digest(AGathering(), "flight-1", T0);
        var other = FactPipeline.Digest(
            new GatheredFacts([new FactPayload.Environment(AnEnvironment() with
            {
                Provenance = EnvironmentProvenance.Reused,
            })]),
            "flight-1", T0);

        await Assert.That(one.Items[0].Digest).IsNotEqualTo(other.Items[0].Digest);
    }

    [Test]
    public async Task Two_flights_produce_different_idempotency_keys_for_the_same_fact()
    {
        // Two runners in identical containers produce the same environment
        // fact. If the keys collided the second flight's fact would dedupe away
        // and that flight would have no environment recorded at all.
        var one = FactPipeline.Digest(AGathering(), "flight-1", T0);
        var other = FactPipeline.Digest(AGathering(), "flight-2", T0);

        await Assert.That(one.Items[0].IdempotencyKey).IsNotEqualTo(other.Items[0].IdempotencyKey);
    }

    [Test]
    public async Task The_filter_is_a_real_stage_that_currently_removes_nothing()
    {
        // Honest about what it is. Classification joins in step 7, when there
        // is content to classify; today it passes everything through and the
        // point of it existing is that step 7 adds a rule rather than a stage.
        var filtered = FactPipeline.Filter(FactPipeline.Digest(AGathering(), "flight-1", T0), "internal");

        await Assert.That(filtered.Items).HasCount(1);
    }

    [Test]
    public async Task An_over_budget_fact_is_refused_here_rather_than_shipped_to_be_refused_there()
    {
        // Ingress rejects it too, and that is the gate that counts. Refusing
        // locally as well means a runner does not spend a round trip finding
        // out something it could compute - and it names the offender while it
        // still has it.
        var enormous = new EnvironmentIdentity
        {
            HostFingerprint = new string('a', 64),
            ImageDigest = null,
            Locks = [.. Enumerable.Range(0, 4000).Select(i => new LockHash
            {
                Path = $"deeply/nested/path/number/{i}/package-lock.json",
                Sha256 = new string('b', 64),
            })],
            Tools = [],
            Provenance = EnvironmentProvenance.Fresh,
        };

        var digested = FactPipeline.Digest(
            new GatheredFacts([new FactPayload.Environment(enormous)]), "flight-1", T0);

        await Assert.That(FactPipeline.OverBudget(digested.Items[0])).IsTrue();

        var filtered = FactPipeline.Filter(digested, "internal");
        await Assert.That(filtered.Items).IsEmpty()
            .Because("nothing is truncated: the whole item is withheld, and it is named where it was made.");
    }

    [Test]
    public async Task A_fact_within_budget_is_not_refused()
    {
        // The other half; without it the assertion above passes on a filter
        // that drops everything.
        var digested = FactPipeline.Digest(AGathering(), "flight-1", T0);

        await Assert.That(FactPipeline.OverBudget(digested.Items[0])).IsFalse();
    }
}

/// <summary>
/// <c>environment.identity</c>: the first real fact.
/// </summary>
/// <remarks>
/// Warm pools are years away and this belongs in slice one anyway: tests passed
/// is not a fact without the environment they passed in, and a laptop is the
/// least reproducible environment in the fleet - which makes it more important
/// locally, not less.
/// </remarks>
public class EnvironmentIdentityTests
{
    [Test]
    public async Task The_environment_reports_a_fingerprint_and_the_tools_it_used()
    {
        using var trees = new ScratchTreeRoot();

        var identity = EnvironmentSurvey.Observe(treePath: null, provenance: EnvironmentProvenance.Fresh);

        await Assert.That(identity.HostFingerprint).HasCount(64);
        await Assert.That(identity.Tools.Select(t => t.Name)).Contains("git")
            .Because("git is the tool that put the source on disk; a run nobody can reproduce is one "
                   + "where nobody recorded which git did it.");
        await Assert.That(identity.Provenance).IsEqualTo(EnvironmentProvenance.Fresh);
    }

    [Test]
    public async Task The_fingerprint_is_stable_across_two_observations_of_one_machine()
    {
        // A fingerprint that moved every time would make every environment look
        // new, which is the same as having no fingerprint.
        var one = EnvironmentSurvey.Observe(treePath: null, provenance: EnvironmentProvenance.Fresh);
        var other = EnvironmentSurvey.Observe(treePath: null, provenance: EnvironmentProvenance.Reused);

        await Assert.That(one.HostFingerprint).IsEqualTo(other.HostFingerprint);
    }

    [Test]
    public async Task Lock_files_in_the_tree_are_recorded_as_a_path_and_a_hash()
    {
        // Paths and hashes. Never the lock file itself - the point is to know
        // whether two runs resolved the same dependencies, and a hash answers
        // that without carrying a customer's dependency graph off the machine.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var materialized = await new Materializer(new Gg.Runner.Vcs.LocalVcsAdapter(), trees.Root)
            .MaterializeAsync("flight-1", new Gg.Runner.Vcs.RepoTarget
            {
                Provider = Gg.Runner.Vcs.LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            }, secret: null);

        var identity = EnvironmentSurvey.Observe(materialized.Path, EnvironmentProvenance.Fresh);

        var recorded = identity.Locks.SingleOrDefault(l => l.Path == "package-lock.json");
        await Assert.That(recorded).IsNotNull();
        await Assert.That(recorded!.Sha256).HasCount(64);
    }

    [Test]
    public async Task No_lock_file_content_is_recorded()
    {
        // The negative half, on the one fact that reads files at all.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var materialized = await new Materializer(new Gg.Runner.Vcs.LocalVcsAdapter(), trees.Root)
            .MaterializeAsync("flight-1", new Gg.Runner.Vcs.RepoTarget
            {
                Provider = Gg.Runner.Vcs.LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            }, secret: null);

        var identity = EnvironmentSurvey.Observe(materialized.Path, EnvironmentProvenance.Fresh);
        var serialized = JsonSerializer.Serialize(identity, JsonSerializerOptions.Web);

        await Assert.That(serialized).DoesNotContain("lockfileVersion")
            .Because("the lock file's contents are in the tree and must stay there.");
        await Assert.That(serialized).Contains("package-lock.json")
            .Because("if the survey did not see the file at all, the absence above proves nothing.");
    }

    [Test]
    public async Task An_image_digest_is_recorded_when_there_is_an_image_and_null_when_there_is_not()
    {
        // Null means "not running in an image", which is a different fact from
        // "running in an image nobody recorded" - and only one of them is true
        // on a laptop.
        await Assert.That(EnvironmentSurvey.Observe(null, EnvironmentProvenance.Fresh, imageDigest: null)
            .ImageDigest).IsNull();
        await Assert.That(EnvironmentSurvey.Observe(null, EnvironmentProvenance.Fresh, imageDigest: "sha256:abc")
            .ImageDigest).IsEqualTo("sha256:abc");
    }

    [Test]
    public async Task An_observed_environment_is_a_valid_fact()
    {
        // The survey and the contract's own rule must agree, or the runner
        // gathers something the control plane refuses.
        var identity = EnvironmentSurvey.Observe(null, EnvironmentProvenance.Fresh);

        var envelope = new FactEnvelope
        {
            IdempotencyKey = "k",
            Kind = FactKinds.EnvironmentIdentity,
            Digest = new string('a', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
            Environment = identity,
        };

        await Assert.That(FactEnvelope.Validate(envelope)).IsNull();
    }
}
