using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A decided <c>build</c> is performed by the pool's own runner: fetch the
/// recipe at the commit its ref resolves to, build it, push it to the pool's
/// registry, and attest the digest and the commit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-one, S41.3-02, rules 10, 11, 12 and 14.</b> The build context
/// is the recipe directory and nothing else; the image goes to the repository
/// part of the strategy's current pin and nowhere a recipe names; it carries a
/// label saying what it was made from; and a failed build pushes nothing and
/// changes nothing.
/// </para>
/// <para>
/// <b>What a failure says, and what it does not.</b> Docker's build output echoes
/// the recipe's own lines - a <c>RUN</c> step, a mirror's hostname - and the
/// attestation crosses to the control plane, which never holds a recipe's words
/// (rule 2). So the attestation says which stage failed, and the output stays
/// in this machine's own log.
/// </para>
/// </remarks>
public class TheMaintainerBuildsFromTheRecipeTests
{
    private const string Pin =
        "127.0.0.1:5000/gg-member@sha256:7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c";

    private const string Commit = "1c26776e2b1f0c3d5a8e9f4b7c6d2a1e0f9b8c7d";

    private const string Pushed = "sha256:9f0e7d6c5b4a39281706f5e4d3c2b1a09f8e7d6c5b4a39281706f5e4d3c2b1a0";

    private sealed class Recipes : IRecipeSource
    {
        public List<PoolRecipe> Asked { get; } = [];

        public RecipeFetch Answer { get; set; } = new RecipeFetch.Fetched("/scratch/recipe", Commit);

        public Task<RecipeFetch> FetchAsync(
            PoolRecipe recipe, string scratchDirectory, CancellationToken cancellationToken = default)
        {
            Asked.Add(recipe);
            return Task.FromResult(Answer);
        }
    }

    private sealed class Builder : IImageBuilder
    {
        public List<(string Context, string Dockerfile, string Tag, IReadOnlyDictionary<string, string> Labels)> Builds { get; } = [];

        public List<(string Repository, string Tag)> Pushes { get; } = [];

        public ImageBuilt Built { get; set; } = new ImageBuilt.Built("sha256:local");

        public ImagePushed Pushing { get; set; } = new ImagePushed.Pushed(Pushed);

        public Task<ImageBuilt> BuildAsync(
            string context, string dockerfile, string tag, IReadOnlyDictionary<string, string> labels,
            CancellationToken cancellationToken = default)
        {
            Builds.Add((context, dockerfile, tag, labels));
            return Task.FromResult(Built);
        }

        public Task<ImagePushed> PushAsync(
            string repository, string tag, CancellationToken cancellationToken = default)
        {
            Pushes.Add((repository, tag));
            return Task.FromResult(Pushing);
        }
    }

    private sealed class Members : IPoolAdapter
    {
        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe { Held = true, ProbedAt = DateTimeOffset.Parse("2026-09-18T22:00:00Z") });

        public Task<IReadOnlyList<PoolMember>> ListAsync(string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>([]);

        public Task<PoolObservation> VerifyAsync(PoolMember member, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, MemberSpec spec, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("a build refreshes no member");

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("a build resets no member");
    }

    private sealed class Protocol : IPoolProtocol
    {
        public Queue<IReadOnlyList<PoolAction>> Served { get; } = [];

        public List<PoolAttestation> Attested { get; } = [];

        public Task<PoolActionList> PullActionsAsync(string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolActionList { Actions = Served.TryDequeue(out var a) ? a : [] });

        public Task<MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberCredentialMinted?>(null);

        public Task AttestAsync(string pool, PoolAttestation attestation, CancellationToken cancellationToken = default)
        {
            Attested.Add(attestation);
            return Task.CompletedTask;
        }
    }

    private static PoolAction ABuild(PoolRecipe? recipe = null) => new()
    {
        ActionId = Guid.Parse("01a0c000-0000-7000-8000-000000000042"),
        Pool = "gg-pool-dev",
        Action = PoolActions.Build,
        Image = Pin,
        StrategyVersion = "dev@v7",
        DecidedAt = DateTimeOffset.Parse("2026-09-18T21:59:00Z"),
        Recipe = recipe ?? new PoolRecipe
        {
            Repository = new LeaseRepoRef { Provider = "ado", Slug = "JDX/gg-airspace", PinnedRef = "main" },
            Path = "images/gg-member",
        },
    };

    private static async Task<(PoolAttestation Build, Recipes Recipes, Builder Builder)> RunAsync(
        PoolAction action, Recipes? recipes = null, Builder? builder = null, bool canBuild = true)
    {
        recipes ??= new Recipes();
        builder ??= new Builder();
        var protocol = new Protocol();
        protocol.Served.Enqueue([action]);
        using var stop = new CancellationTokenSource();

        var loop = new MaintainLoop(
            protocol, new Members(), new MovableClock(DateTimeOffset.Parse("2026-09-18T22:00:00Z")),
            (_, _) =>
            {
                stop.Cancel();
                return Task.CompletedTask;
            },
            recipes: canBuild ? recipes : null,
            builder: canBuild ? builder : null);

        _ = await loop.RunAsync("gg-pool-dev", stop.Token);

        return (protocol.Attested.Single(a => a.Action == PoolActions.Build), recipes, builder);
    }

    [Test]
    public async Task A_decided_build_is_fetched_built_pushed_and_attested_with_its_digest_and_commit()
    {
        var (build, recipes, builder) = await RunAsync(ABuild());

        await Assert.That(recipes.Asked.Single().Path).IsEqualTo("images/gg-member");

        var (context, dockerfile, tag, labels) = builder.Builds.Single();
        await Assert.That(context).IsEqualTo("/scratch/recipe")
            .Because("the build context is the recipe directory the fetch produced, and nothing "
                   + "else - rule 10.");
        await Assert.That(dockerfile).IsEqualTo("Dockerfile");
        await Assert.That(tag).StartsWith("127.0.0.1:5000/gg-member:")
            .Because("the pool's registry is the image's own - the repository part of the pin, "
                   + "and nowhere a recipe could name. Rule 11.");
        await Assert.That(labels["gg.built-from"]).IsEqualTo($"JDX/gg-airspace@{Commit}:images/gg-member")
            .Because("a host's docker inspect answers what an image was made from without asking "
                   + "the control plane - rule 12.");

        await Assert.That(builder.Pushes.Single().Repository).IsEqualTo("127.0.0.1:5000/gg-member");

        await Assert.That(build.Outcome).IsEqualTo(PoolOutcomes.Verified);
        await Assert.That(build.ImageDigest).IsEqualTo(Pushed)
            .Because("the digest the registry answered with, which is what a pin can name.");
        await Assert.That(build.RecipeCommit).IsEqualTo(Commit);
        await Assert.That(PoolAttestation.Validate(build)).IsNull();
    }

    [Test]
    public async Task A_named_dockerfile_is_the_one_built()
    {
        var named = ABuild() with
        {
            Recipe = ABuild().Recipe! with { Dockerfile = "Dockerfile.dev" },
        };

        var (_, _, builder) = await RunAsync(named);

        await Assert.That(builder.Builds.Single().Dockerfile).IsEqualTo("Dockerfile.dev");
    }

    [Test]
    public async Task A_recipe_that_would_not_fetch_is_attested_failed_and_nothing_is_built()
    {
        var recipes = new Recipes
        {
            Answer = new RecipeFetch.Refused("'main' could not be resolved in JDX/gg-airspace."),
        };

        var (build, _, builder) = await RunAsync(ABuild(), recipes);

        await Assert.That(build.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(build.Diagnosis!).Contains("could not be resolved");
        await Assert.That(builder.Builds).IsEmpty();
        await Assert.That(builder.Pushes).IsEmpty();
    }

    [Test]
    public async Task A_failed_build_pushes_nothing_and_keeps_its_output_on_this_machine()
    {
        // THE OUTPUT ECHOES THE RECIPE. A failed RUN step prints the command
        // and whatever it touched; carrying that on the attestation would put a
        // recipe's words on the control plane, which never holds them - rule 2.
        var builder = new Builder
        {
            Built = new ImageBuilt.Failed(
                "the recipe did not build: a step failed.",
                "The command '/bin/sh -c curl http://mirror.internal/secret-tool' returned 22"),
        };

        var (build, _, _) = await RunAsync(ABuild(), builder: builder);

        await Assert.That(build.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(build.Diagnosis!).Contains("did not build");
        await Assert.That(build.Diagnosis!).DoesNotContain("mirror.internal")
            .Because("the daemon's detail is the recipe's text, and it stays in this machine's "
                   + "log rather than crossing.");
        await Assert.That(builder.Pushes).IsEmpty()
            .Because("a build that failed has nothing to push - rule 14.");
        await Assert.That(build.RecipeCommit).IsEqualTo(Commit)
            .Because("which commit failed to build is the first thing somebody fixing it needs.");
    }

    [Test]
    public async Task A_refused_push_is_attested_failed()
    {
        var builder = new Builder
        {
            Pushing = new ImagePushed.Failed("the registry refused the push.", "unauthorized"),
        };

        var (build, _, _) = await RunAsync(ABuild(), builder: builder);

        await Assert.That(build.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(build.ImageDigest).IsNull()
            .Because("nothing is in the registry to pin.");
    }

    [Test]
    public async Task A_build_without_a_recipe_does_nothing_and_says_so()
    {
        var (build, recipes, builder) = await RunAsync(ABuild() with { Recipe = null });

        await Assert.That(build.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(recipes.Asked).IsEmpty();
        await Assert.That(builder.Builds).IsEmpty();
    }

    [Test]
    public async Task A_runner_started_without_a_way_to_build_says_that_rather_than_nothing()
    {
        var (build, _, _) = await RunAsync(ABuild(), canBuild: false);

        await Assert.That(build.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(build.Diagnosis!).Contains("build")
            .Because("a maintainer that silently skipped a decided build would leave the "
                   + "control plane waiting on an answer that never comes.");
    }
}
