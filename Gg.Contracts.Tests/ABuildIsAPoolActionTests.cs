using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>build</c> is a fifth pool action: decided by the control plane, performed
/// by the pool's own runner, and classified an outward act.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-one, S41.2-01.</b> A strategy may name its recipe (0.194.0);
/// this is how a build of it is asked for and answered. The action carries the
/// recipe resolved to where the runner can fetch it, and the attestation carries
/// the commit the runner built from and the digest it pushed.
/// </para>
/// <para>
/// <b>Outward, and step 0 is why.</b> The kinds table defines record-only as
/// <i>"the attestation is its whole product"</i>, and a build's product is an
/// image in a registry. It also runs a recipe on the pool host's daemon through
/// a proxy that refused it until the slice gave it two narrow allowances - an
/// allowance the scope probe has to prove before anything is decided toward it.
/// That is exactly what an outward act is gated on.
/// </para>
/// </remarks>
public class ABuildIsAPoolActionTests
{
    private const string Commit = "1c26776e2b1f0c3d5a8e9f4b7c6d2a1e0f9b8c7d";

    private static Guid V7() => Guid.CreateVersion7();

    private static PoolAttestation ABuild() => new()
    {
        AttestationId = V7(),
        Pool = "gg-pool-dev",
        Action = PoolActions.Build,
        Outcome = PoolOutcomes.Verified,
        ImageDigest = "sha256:7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c",
        RecipeCommit = Commit,
        MeasuredAt = DateTimeOffset.Parse("2026-09-18T22:00:00Z"),
    };

    [Test]
    public async Task Build_is_a_pool_action_and_an_outward_act()
    {
        await Assert.That(PoolActions.Build).IsEqualTo("build");
        await Assert.That(PoolActions.All).Contains(PoolActions.Build);

        await Assert.That(PoolActionKinds.Of(PoolActions.Build)).IsEqualTo(PoolActionKinds.OutwardAct)
            .Because("a build's product is an image in a registry, not its attestation, and "
                   + "it passes a proxy scope the probe must prove - which is what the "
                   + "decider gates an outward act on.");
    }

    [Test]
    public async Task A_decided_build_carries_where_its_recipe_is()
    {
        // THE COORDINATE TYPE THE RUNNER'S VCS PORT ALREADY TAKES, as a sweep's
        // skill does since 0.187.0 - the control plane resolves the registry
        // name to a provider and a slug when it decides, and the name never
        // leaves this side.
        var action = new PoolAction
        {
            ActionId = V7(),
            Pool = "gg-pool-dev",
            Action = PoolActions.Build,
            Image = "127.0.0.1:5000/gg-member@sha256:"
                  + "7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c",
            StrategyVersion = "dev@v7",
            DecidedAt = DateTimeOffset.Parse("2026-09-18T22:00:00Z"),
            Recipe = new PoolRecipe
            {
                Repository = new LeaseRepoRef
                {
                    Provider = "ado",
                    Slug = "JDX/gg-airspace",
                    PinnedRef = "main",
                },
                Path = "images/gg-member",
                Dockerfile = null,
            },
        };

        await Assert.That(action.Recipe!.Repository.PinnedRef).IsEqualTo("main")
            .Because("a ref, which the runner resolves - the commit comes back on the "
                   + "attestation rather than being fixed here.");
    }

    [Test]
    public async Task A_good_build_says_what_it_built_from_and_what_it_pushed()
    {
        await Assert.That(PoolAttestation.Validate(ABuild())).IsNull();

        await Assert.That(PoolAttestation.Validate(ABuild() with { RecipeCommit = null }))
            .IsNotNull()
            .Because("a build that will not say which commit it built is an image nobody can "
                   + "trace back to a reviewed recipe.");

        await Assert.That(PoolAttestation.Validate(ABuild() with { ImageDigest = null }))
            .IsNotNull()
            .Because("a build that pushed nothing it can name has nothing to move the pin to.");
    }

    [Test]
    public async Task The_commit_is_a_commit_and_never_the_ref()
    {
        var refused = PoolAttestation.Validate(ABuild() with { RecipeCommit = "main" });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("main");
    }

    [Test]
    public async Task A_failed_build_needs_only_its_reason()
    {
        // A REF THAT WOULD NOT RESOLVE has no commit to report and pushed
        // nothing; the reason is the whole of what it can say.
        var failed = ABuild() with
        {
            Outcome = PoolOutcomes.Failed,
            RecipeCommit = null,
            ImageDigest = null,
            Diagnosis = "'main' could not be resolved in JDX/gg-airspace.",
        };

        await Assert.That(PoolAttestation.Validate(failed)).IsNull();
    }

    [Test]
    public async Task Only_a_build_says_it_built_from_a_commit()
    {
        var refresh = ABuild() with { Action = PoolActions.Refresh };

        await Assert.That(PoolAttestation.Validate(refresh)).IsNotNull()
            .Because("a recipe commit on a refresh is a claim about a build that did not "
                   + "happen, and a reader of the ledger would believe it.");
    }

    [Test]
    public async Task The_wire_carries_the_recipe_and_the_commit()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(PoolAction)]).Contains("recipe");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(PoolAttestation)])
            .Contains("recipeCommit");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(PoolRecipe)])
            .IsEquivalentTo((string[])["repository", "path", "dockerfile"]);
    }

    [Test]
    public async Task A_person_asks_for_a_build_through_a_declared_door()
    {
        var door = ProtocolSurface.Endpoints.Single(e =>
            e.Method == "POST" && e.Path == "/v1/airspace/strategies/{name}/builds");

        await Assert.That(door.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(door.Response).IsEqualTo(typeof(PoolAction))
            .Because("what was decided is the answer: the build, and the recipe it will fetch.");
        await Assert.That(door.Statuses).Contains(202);
        await Assert.That(door.Statuses).Contains(404)
            .Because("a strategy that names no recipe has nothing to build.");
        await Assert.That(door.Statuses).Contains(409)
            .Because("one build per strategy at a time - slice forty-one rule 13.");
    }
}
