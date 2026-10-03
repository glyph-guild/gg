using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A relationship in the vocabulary and not in the product is refused at apply.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three values so the schema does not move the day a worker is remote, and
/// two of them refused until something implements them.</b> ADR-0033's first
/// amendment found that sync <i>"mostly disappears"</i> because the tree and the
/// daemon share a filesystem — but it holds that only <i>"while the worker is
/// co-located with the environment; a remote worker brings the pull back"</i>.
/// Declaring all three now means the vocabulary is right before the product is;
/// refusing two means nobody can write a document that silently does nothing.
/// </para>
/// <para>
/// <b>Refused where the author can act.</b> The alternative is a document that
/// applies cleanly and fails at the first flight that needs it — which is a
/// person finding out from a stranded flight rather than from the thing they
/// just wrote. Same disposition as an unpinned image, and for the same reason.
/// </para>
/// <para>
/// <b>And `shared` obliges no sync hook, which is the half of this that belongs
/// in the contract.</b> Whether a sync hook is NEEDED is a question about the
/// declaration and is answered here. Whether one is PRESENT in a directory
/// nothing will run is a question about a filesystem, which this package cannot
/// see — it is S58.5-02, on the side that reads the directory.
/// </para>
/// </remarks>
public class AnUnbuiltRelationshipIsRefusedTests
{
    private static string Strategy(string filesystem) => $"""
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
        hooks: ".goodgrief/environments/ui"
        filesystem: {filesystem}
        """;

    [Test]
    public async Task The_three_relationships_are_declared_and_in_All()
    {
        await Assert.That(FilesystemRelationships.All).IsEquivalentTo(new[]
        {
            FilesystemRelationships.Shared,
            FilesystemRelationships.Push,
            FilesystemRelationships.Pull,
        });
    }

    [Test]
    public async Task Shared_is_taken()
    {
        var parsed = EnvelopeYaml.ParseStrategy(Strategy(FilesystemRelationships.Shared));

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because($"`shared` is the one the product implements: {parsed.Diagnosis}");
    }

    [Test]
    public async Task Push_and_pull_are_refused_naming_the_relationship()
    {
        foreach (var unbuilt in (string[])
            [FilesystemRelationships.Push, FilesystemRelationships.Pull])
        {
            var parsed = EnvelopeYaml.ParseStrategy(Strategy(unbuilt));

            await Assert.That(parsed.Diagnosis).IsNotNull()
                .Because($"'{unbuilt}' is in the vocabulary and not in the product, and a "
                       + "document that applied would do nothing at the first flight that "
                       + "needed it.");

            await Assert.That(parsed.Diagnosis!).Contains(unbuilt)
                .Because("named, so an author reads which of the three they chose rather "
                       + "than being told the member is wrong.");
        }
    }

    [Test]
    public async Task A_relationship_nobody_declared_is_refused_naming_what_was_expected()
    {
        var parsed = EnvelopeYaml.ParseStrategy(Strategy("rsync"));

        await Assert.That(parsed.Diagnosis).IsNotNull();
        await Assert.That(parsed.Diagnosis!).Contains(FilesystemRelationships.Shared)
            .Because("an unknown word is told the set, because the author's next move is to "
                   + "pick from it.");
    }

    [Test]
    public async Task Shared_obliges_no_sync_hook_and_the_other_two_do()
    {
        // THE DECLARATION HALF of S58.1-04. A `shared` environment writes no
        // sync hook at all rather than an empty one - an always-empty hook
        // teaches an agent to produce empty files, and `shared` says the same
        // thing once where a reader can see it.
        await Assert.That(FilesystemRelationships.NeedsSyncHook(FilesystemRelationships.Shared))
            .IsFalse()
            .Because("the tree and the daemon share a filesystem, so there is nothing to "
                   + "ship and no hook to write.");

        foreach (var moving in (string[])
            [FilesystemRelationships.Push, FilesystemRelationships.Pull])
        {
            await Assert.That(FilesystemRelationships.NeedsSyncHook(moving)).IsTrue()
                .Because($"'{moving}' means the tree has to be moved, and the thing that "
                       + "moves it is the hook.");
        }

        await Assert.That(FilesystemRelationships.NeedsSyncHook(null)).IsFalse()
            .Because("a strategy with no declared relationship has no hooks either, so "
                   + "nothing is owed.");
    }
}
