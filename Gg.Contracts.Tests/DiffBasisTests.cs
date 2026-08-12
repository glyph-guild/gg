using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A change manifest says which diff it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>The data was true and the label was false.</b> What the runner computes
/// is a two-point diff between the base commit and the head, and it is
/// presented as "this pull request's change". Those differ whenever anything
/// landed on the base branch after the branch point: the manifest over-reports
/// by everything somebody else merged, and nothing anywhere said so.
/// </para>
/// <para>
/// This is the same failure the oversize case already avoids. A rollup does
/// not pretend to be a file list - it says <c>directories</c> and states what
/// it summarises. A two-point diff pretending to be a merge-base diff is that
/// bug without the honesty.
/// </para>
/// <para>
/// <b>Labelled, not fixed.</b> Computing a real merge base is out of scope for
/// this step; the point is that a consumer can tell which one it is holding,
/// and that the day somebody computes one, the label moves rather than the
/// meaning of the old facts changing underneath everybody.
/// </para>
/// </remarks>
public class DiffBasisTests
{
    private static ChangeManifest AManifest(string? basis = null) => new()
    {
        BaseCommit = new string('a', 40),
        HeadCommit = new string('b', 40),
        Resolution = ChangeResolution.Files,
        DiffBasis = basis ?? Gg.Contracts.DiffBasis.TwoPoint,
        Paths = [],
        Directories = [],
        Languages = [],
        FilesChanged = 0,
        LinesAdded = 0,
        LinesRemoved = 0,
        PathsWithheld = 0,
    };

    [Test]
    public async Task The_two_bases_are_the_two_that_exist_and_no_others()
    {
        await Assert.That(Gg.Contracts.DiffBasis.All)
            .IsEquivalentTo((string[])["two-point", "merge-base"]);
    }

    [Test]
    public async Task A_manifest_must_say_which_one_it_is()
    {
        // Validate is where every other unreadable manifest is refused, and a
        // basis nothing recognises is exactly as unreadable as an unknown
        // resolution.
        var diagnosis = ChangeManifest.Validate(AManifest(basis: "guessed"));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!.ToLowerInvariant()).Contains("basis");
    }

    [Test]
    public async Task A_manifest_that_says_which_one_it_is_passes()
    {
        // The twin. Without it, "an unknown basis is refused" also passes on a
        // Validate that refuses everything.
        foreach (var basis in Gg.Contracts.DiffBasis.All)
        {
            await Assert.That(ChangeManifest.Validate(AManifest(basis))).IsNull()
                .Because($"'{basis}' is one of the two this vocabulary defines.");
        }
    }

    [Test]
    public async Task What_the_runner_produces_today_is_labelled_two_point()
    {
        // Honest about what it is rather than about what we would like it to
        // be. A manifest labelled merge-base that was computed two-point would
        // be worse than the unlabelled version, because somebody would trust
        // it.
        await Assert.That(Gg.Contracts.DiffBasis.TwoPoint).IsEqualTo("two-point");
    }

    [Test]
    public async Task The_basis_is_not_the_resolution()
    {
        // Two independent facts about one manifest and it is worth saying so:
        // a rollup can be either basis, and a file list can be either basis.
        // Collapsing them into one field would make "we fixed the basis" and
        // "the change got smaller" the same edit.
        var rollup = AManifest(Gg.Contracts.DiffBasis.MergeBase) with
        {
            Resolution = ChangeResolution.Directories,
            Directories = [new DirectoryChange { Directory = "src", Files = 1, LinesAdded = 1, LinesRemoved = 0 }],
            FilesChanged = 1,
            LinesAdded = 1,
        };

        await Assert.That(ChangeManifest.Validate(rollup)).IsNull();
        await Assert.That(rollup.DiffBasis).IsEqualTo(Gg.Contracts.DiffBasis.MergeBase);
        await Assert.That(rollup.Resolution).IsEqualTo(ChangeResolution.Directories);
    }

    [Test]
    public async Task The_fact_vocabulary_moved_with_the_shape()
    {
        // The mechanism that exists so a runner and a control plane cannot
        // disagree about what a fact means. Adding a required member to a
        // pinned fact type is exactly the change it is for.
        //
        // Asserted as "at least the version this shape arrived in" rather than
        // as a literal. DiffBasis landed in 0.5.0 and the vocabulary has moved
        // since; pinning the literal here made every later fact type edit this
        // test, which teaches somebody to edit it without reading it.
        await Assert.That(string.CompareOrdinal(FactVocabulary.Version, "0.5.0"))
            .IsGreaterThanOrEqualTo(0)
            .Because("DiffBasis arrived in 0.5.0, so the vocabulary can never be older than that.");
    }
}
