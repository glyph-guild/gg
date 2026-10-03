using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A strategy may say where its hooks are and how its filesystem relates.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both optional, and absent means it has no hooks.</b> Every strategy
/// written before slice fifty-eight declares neither, and must keep parsing —
/// the same disposition <see cref="EnvironmentStrategy.Build"/> took when it
/// arrived for strategies whose image was pinned by hand.
/// </para>
/// <para>
/// <b>On the strategy because the strategy IS the environment's declaration.</b>
/// It already names the charted environment, the pool that furnishes it and the
/// image every instance is reset to; where the stack inside comes up from
/// belongs beside those and not in a document of its own.
/// </para>
/// <para>
/// <b>A path, never a script body.</b> The contract carries where to look; what
/// is there is the customer's, read by the runner on the host that holds the
/// instance. A contract that carried the script would be the control plane
/// holding customer code, which is the one boundary this package exists to keep.
/// </para>
/// </remarks>
public class AnEnvironmentMaySayHowItComesUpTests
{
    private static string Strategy(string extra = "") => $"""
        kind: docker-host
        environment: ui
        inventory:
          pool: gg-pool-ui
          size: 2
          warm: 2
        pull-point: resident-runner
        image: "127.0.0.1:5000/gg-member-browser@sha256:7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c"
        bounds:
          pool-max: 2
        {extra}
        """;

    [Test]
    public async Task A_strategy_declaring_neither_still_parses()
    {
        // EVERY STRATEGY IN THE FIELD. Both of this tenant's were written before
        // the five points existed, and a required member here would have refused
        // the documents that are live right now.
        var parsed = EnvelopeYaml.ParseStrategy(Strategy());

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because($"a strategy that says nothing about hooks has none: {parsed.Diagnosis}");

        await Assert.That(parsed.Strategy!.Hooks).IsNull();
        await Assert.That(parsed.Strategy!.Filesystem).IsNull()
            .Because("absent is absent, and a defaulted `shared` here would claim a "
                   + "relationship nobody declared.");
    }

    [Test]
    public async Task A_strategy_may_say_where_its_hooks_are_and_how_its_filesystem_relates()
    {
        var parsed = EnvelopeYaml.ParseStrategy(Strategy("""
            hooks: ".goodgrief/environments/ui"
            filesystem: shared
            """));

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because($"this is the declaration slice fifty-eight exists for: {parsed.Diagnosis}");

        await Assert.That(parsed.Strategy!.Hooks).IsEqualTo(".goodgrief/environments/ui");
        await Assert.That(parsed.Strategy!.Filesystem)
            .IsEqualTo(FilesystemRelationships.Shared);
    }

    [Test]
    public async Task Both_survive_the_writer()
    {
        // ROUND TRIP, because a member the writer drops vanishes the first time
        // anything re-renders the document - and the control plane stores a
        // strategy and renders it back for `gg airspace pull`. A reader on an
        // older gg silently dropping a new member is not hypothetical: it is
        // what made an `environment:` advice key look empty two slices ago.
        var first = EnvelopeYaml.ParseStrategy(Strategy("""
            hooks: ".goodgrief/environments/ui"
            filesystem: shared
            """));

        await Assert.That(first.Diagnosis).IsNull();

        var again = EnvelopeYaml.ParseStrategy(EnvelopeText.Render(first.Strategy!));

        await Assert.That(again.Diagnosis).IsNull();
        await Assert.That(again.Strategy!.Hooks).IsEqualTo(".goodgrief/environments/ui");
        await Assert.That(again.Strategy!.Filesystem).IsEqualTo(FilesystemRelationships.Shared);
    }

    [Test]
    public async Task A_filesystem_relationship_with_no_hooks_is_refused()
    {
        // A RELATIONSHIP FOR HOOKS THAT DO NOT EXIST. `filesystem` says how gg
        // should get the tree to the place the hooks run; with no hooks there is
        // no place and nothing to get. Refused where the author can act, rather
        // than carried as a member nothing reads.
        var parsed = EnvelopeYaml.ParseStrategy(Strategy("filesystem: shared"));

        await Assert.That(parsed.Diagnosis).IsNotNull()
            .Because("a filesystem relationship is about where hooks run, so one declared "
                   + "without hooks describes nothing.");

        await Assert.That(parsed.Diagnosis!).Contains("hooks")
            .Because("and it names the member that is missing rather than the one that is "
                   + "present, because the missing one is what the author has to add.");
    }
}
