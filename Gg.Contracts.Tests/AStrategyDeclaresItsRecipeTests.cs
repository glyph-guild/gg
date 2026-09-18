using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A strategy may say where its image comes from - a directory in a registered
/// repository - and what the pin in force was built from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-one, S41.1-01.</b> A strategy pins its image by digest, and
/// nothing said where that image came from. On the dev tenant's pool host one
/// recipe lived in a checkout of this repository and the other in a directory
/// on the host that no repository held - and the dev image shipped a
/// hand-copied gg 0.1.0 for two weeks with nothing the platform held able to
/// say so.
/// </para>
/// <para>
/// <b>The digest stays the pin.</b> <c>build:</c> is where the NEXT pin comes
/// from and is never read when a member is made, so every rule about
/// <c>image:</c> holds unchanged beside it. <c>built-from:</c> is the record of
/// what the pin in force was made from, and it only means anything beside the
/// recipe it names.
/// </para>
/// <para>
/// <b>By reference, never by content.</b> A recipe is a repository's registry
/// name, a path and a ref - the shape a watch's skill already has - and a
/// Dockerfile's text has no member to arrive in. Dockerfiles carry internal
/// hosts and package mirrors, and the boundary is the one customer code sits
/// behind.
/// </para>
/// </remarks>
public class AStrategyDeclaresItsRecipeTests
{
    private const string Commit = "1c26776e2b1f0c3d5a8e9f4b7c6d2a1e0f9b8c7d";

    private static EnvironmentStrategy Plain() => new()
    {
        Kind = StrategyKinds.DockerHost,
        Environment = "dev",
        Inventory = new StrategyInventory { Pool = "gg-pool-dev", Size = 2, Warm = 1 },
        PullPoint = PullPoints.ResidentRunner,
        Image = "127.0.0.1:5000/gg-member@sha256:"
              + "7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c",
        Bounds = new StrategyBounds { PoolMax = 2 },
    };

    private static StrategyBuild Recipe() => new()
    {
        Repository = "gg-airspace",
        Path = "images/gg-member",
        Ref = "main",
    };

    private static EnvironmentStrategy WithRecipe() => Plain() with
    {
        Build = Recipe(),
        BuiltFrom = new StrategyProvenance
        {
            Repository = "gg-airspace",
            Path = "images/gg-member",
            Commit = Commit,
        },
    };

    // ---- what may be declared ----

    [Test]
    public async Task A_strategy_that_names_its_recipe_is_valid()
    {
        await Assert.That(EnvironmentStrategy.Validate(WithRecipe())).IsNull();

        await Assert.That(EnvironmentStrategy.Validate(Plain() with { Build = Recipe() }))
            .IsNull()
            .Because("a recipe with nothing built from it yet is the state every strategy is "
                   + "in on the day it first names one.");
    }

    [Test]
    public async Task A_strategy_with_no_recipe_is_what_it_was()
    {
        await Assert.That(EnvironmentStrategy.Validate(Plain())).IsNull()
            .Because("every strategy in force names no recipe, and none of them may become "
                   + "invalid the day this ships.");
    }

    [Test]
    [Arguments("repository")]
    [Arguments("path")]
    [Arguments("ref")]
    public async Task A_recipe_missing_a_part_is_refused_naming_it(string part)
    {
        var recipe = part switch
        {
            "repository" => Recipe() with { Repository = " " },
            "path" => Recipe() with { Path = "" },
            _ => Recipe() with { Ref = " " },
        };

        var refused = EnvironmentStrategy.Validate(Plain() with { Build = recipe });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains($"build.{part}")
            .Because("the refusal names the key somebody has to fill in.");
    }

    [Test]
    [Arguments("../outside")]
    [Arguments("images/../../outside")]
    [Arguments("/etc")]
    [Arguments("images\\gg-member")]
    public async Task A_recipe_path_that_could_leave_the_repository_is_refused(string path)
    {
        // THE BUILD CONTEXT IS THE DIRECTORY, AND NOTHING ELSE. A path that can
        // climb out of the repository, or names a place on whatever machine
        // reads it, is a build context with no containment at all.
        var refused = EnvironmentStrategy.Validate(
            Plain() with { Build = Recipe() with { Path = path } });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("build.path");
    }

    [Test]
    public async Task A_dockerfile_that_could_leave_the_directory_is_refused()
    {
        var refused = EnvironmentStrategy.Validate(
            Plain() with { Build = Recipe() with { Dockerfile = "../Dockerfile" } });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("build.dockerfile");

        await Assert.That(EnvironmentStrategy.Validate(
                Plain() with { Build = Recipe() with { Dockerfile = "Dockerfile.dev" } }))
            .IsNull()
            .Because("a named Dockerfile inside the directory is what the member exists for.");
    }

    [Test]
    public async Task The_image_is_still_pinned_by_digest_beside_a_recipe()
    {
        // RULE 1. A recipe does not make a tag acceptable: the pin is what a
        // member is made from, and a tag is whatever it means today.
        var tagged = WithRecipe() with { Image = "127.0.0.1:5000/gg-member:v5" };

        await Assert.That(EnvironmentStrategy.Validate(tagged)).IsNotNull();
    }

    // ---- what the pin was built from ----

    [Test]
    public async Task Built_from_without_a_recipe_is_refused()
    {
        var refused = EnvironmentStrategy.Validate(Plain() with { BuiltFrom = WithRecipe().BuiltFrom });

        await Assert.That(refused).IsNotNull()
            .Because("provenance with no recipe beside it is a claim about a build nothing "
                   + "on this document could have asked for.");
        await Assert.That(refused!).Contains("built-from");
    }

    [Test]
    public async Task Built_from_naming_another_recipe_is_refused()
    {
        var elsewhere = WithRecipe() with
        {
            BuiltFrom = WithRecipe().BuiltFrom! with { Path = "images/gg-member-browser" },
        };
        var otherRepository = WithRecipe() with
        {
            BuiltFrom = WithRecipe().BuiltFrom! with { Repository = "gg" },
        };

        await Assert.That(EnvironmentStrategy.Validate(elsewhere)).IsNotNull()
            .Because("an image built from a different directory is not this recipe's, and "
                   + "saying it is would be exactly the claim the direction rule trusts.");
        await Assert.That(EnvironmentStrategy.Validate(otherRepository)).IsNotNull();
    }

    [Test]
    public async Task Built_from_names_a_commit_and_never_a_ref()
    {
        // THE RUNNER RESOLVES THE REF AND REPORTS THE COMMIT, and reporting the
        // ref back is the one answer that looks like an answer and is not.
        var refused = EnvironmentStrategy.Validate(WithRecipe() with
        {
            BuiltFrom = WithRecipe().BuiltFrom! with { Commit = "main" },
        });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("built-from.commit");
    }

    // ---- the tree ----

    [Test]
    public async Task A_recipe_and_its_provenance_render_and_read_back_unchanged()
    {
        var text = EnvelopeText.Render(WithRecipe());
        var parsed = EnvelopeYaml.ParseStrategy(text);

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because($"the renderer's own output must parse: {parsed.Diagnosis}");
        await Assert.That(parsed.Strategy).IsEqualTo(WithRecipe());

        var named = WithRecipe() with { Build = Recipe() with { Dockerfile = "Dockerfile.dev" } };
        await Assert.That(EnvelopeYaml.ParseStrategy(EnvelopeText.Render(named)).Strategy)
            .IsEqualTo(named);
    }

    [Test]
    public async Task A_strategy_with_no_recipe_renders_no_new_line()
    {
        // THE PULL IS WHY, as it was for `warm`. Every strategy in force names
        // no recipe, and a rendering that grew a line for it would make the
        // first `gg airspace pull` after this ships report a change nobody made.
        var text = EnvelopeText.Render(Plain());

        await Assert.That(text).DoesNotContain("build");
        await Assert.That(EnvelopeYaml.ParseStrategy(text).Strategy).IsEqualTo(Plain());
    }

    [Test]
    [Arguments("repository")]
    [Arguments("path")]
    [Arguments("ref")]
    [Arguments("commit")]
    public async Task A_rendering_that_dropped_a_recipe_key_would_be_caught(string key)
    {
        // THE POISON TWIN, StrategyRoundTripTests' own: a key that can go
        // missing between a pull and the apply after it without the parse
        // refusing or the model changing is a key nothing is testing.
        var text = EnvelopeText.Render(WithRecipe());
        var poisoned = string.Join('\n', text.Split('\n')
            .Where(line => !line.TrimStart().StartsWith(key + ":", StringComparison.Ordinal)));

        await Assert.That(poisoned).IsNotEqualTo(text)
            .Because($"'{key}' is not in the rendering at all, so this twin proves nothing");

        var parsed = EnvelopeYaml.ParseStrategy(poisoned);

        await Assert.That(parsed.Diagnosis is not null || parsed.Strategy != WithRecipe()).IsTrue();
    }

    [Test]
    public async Task A_key_the_recipe_does_not_have_is_refused()
    {
        var text = EnvelopeText.Render(Plain() with { Build = Recipe() })
            .Replace("  ref: main\n", "  ref: main\n  context: .\n", StringComparison.Ordinal);

        await Assert.That(EnvelopeYaml.ParseStrategy(text).Diagnosis).IsNotNull()
            .Because("a closed map is what stops a misspelt key being read as absent.");
    }

    // ---- the wire ----

    [Test]
    public async Task The_wire_carries_both_by_name()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvironmentStrategy)])
            .Contains("build");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvironmentStrategy)])
            .Contains("builtFrom");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(StrategyBuild)])
            .IsEquivalentTo((string[])["repository", "path", "ref", "dockerfile"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(StrategyProvenance)])
            .IsEquivalentTo((string[])["repository", "path", "commit"])
            .Because("and nothing a Dockerfile's text could ride in on - rule 2.");
    }
}
