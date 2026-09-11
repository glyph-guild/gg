using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Pull refuses while the working copy holds changes nobody has applied,
/// rather than rendering over them.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED, AND IT COST SOMEBODY TWO DOCUMENTS.</b> A person authored a
/// work kind, applied, committed, and pulled. The pull deleted the work kind
/// outright and reverted the root document's edit to the applied value. Both
/// were recovered from git; neither should have needed recovering.
/// </para>
/// <para>
/// <b>Two mechanisms, one cause.</b> <c>Write</c> renders the estate and
/// <c>Prune</c> then deletes every gg-shaped file the estate did not account
/// for — so a document authored but never applied is indistinguishable from
/// one that was retired. And a document whose local edit has not landed is
/// simply overwritten, because rendering canonical output is what pull is for.
/// </para>
/// <para>
/// <b>THE DIRTY CHECK COULD NOT SEE EITHER, AND THE TOOL'S OWN ADVICE IS WHY.</b>
/// Pull refuses a dirty tree — <i>"refuse, name the files, and let the person
/// commit or discard"</i> — so the person committed, exactly as told. Committing
/// is what left nothing uncommitted to refuse over, and the destruction went
/// through in silence.
/// </para>
/// <para>
/// <b>So the check moves from "uncommitted" to "unapplied", which is the
/// question that was always being asked.</b> The diff already computes it, and
/// after an apply it is empty — so the ordinary path is untouched and only the
/// case that loses work is stopped. Discarding stays git's job, which is the
/// same answer the dirty refusal gives.
/// </para>
/// </remarks>
public class PullDoesNotDestroyUnappliedWorkTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static FlightCommands Build(StubControlPlane stub) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(ASession()));

    [Test]
    public async Task A_document_nobody_applied_is_not_pruned_away()
    {
        // EXACTLY WHAT HAPPENED. The estate holds no document for this name,
        // so the old Prune deleted the file as one it could not account for.
        await using var stub = new StubControlPlane();
        var tree = AnAirspaceTreeOnDisk.WithAWorkKind();
        var path = Path.Combine(
            tree.FullName, "airspace", "work-kinds", "score-hal.yaml");

        try
        {
            var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
                async () => await Build(stub).AirspacePullAsync(tree.FullName));

            await Assert.That(refused!.Message)
                .Contains("score-hal", StringComparison.Ordinal)
                .Because("named, so the person knows which of their documents pull was "
                       + "about to render over.");

            await Assert.That(File.Exists(path)).IsTrue()
                .Because("AND IT IS STILL THERE. A refusal that had already deleted the "
                       + "file would be a worse version of the defect: the same loss, with "
                       + "a sentence about it.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_refusal_says_what_to_do_about_it()
    {
        await using var stub = new StubControlPlane();
        var tree = AnAirspaceTreeOnDisk.WithAWorkKind();

        try
        {
            var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
                async () => await Build(stub).AirspacePullAsync(tree.FullName));

            await Assert.That(refused!.Message)
                .Contains("apply", StringComparison.OrdinalIgnoreCase)
                .Because("the two ways out are applying the change or discarding it, and a "
                       + "refusal naming neither is a wall.");

            await Assert.That(refused.Message)
                .Contains("git", StringComparison.OrdinalIgnoreCase)
                .Because("and discarding is git's job here, which is the answer the dirty "
                       + "refusal already gives - this is a git repository and it behaves "
                       + "like one.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_working_copy_that_matches_still_pulls()
    {
        // THE ORDINARY PATH, UNTOUCHED. After an apply the diff is empty, which
        // is when somebody pulls to get the canonical rendering - so the new
        // check must be invisible there or it has replaced one broken verb
        // with another.
        await using var stub = new StubControlPlane();
        var tree = Directory.CreateTempSubdirectory("gg-clean-pull-");

        try
        {
            var result = await Build(stub).AirspacePullAsync(tree.FullName);

            await Assert.That(result).IsTypeOf<VerbResult.AirspacePulled>()
                .Because("an empty tree has nothing unapplied in it, so there is nothing to "
                       + "protect and the pull is the whole point.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task It_refuses_before_writing_anything_at_all()
    {
        // NOT PART WAY THROUGH. Write renders every document and prunes at the
        // end, so a check inside it would leave a tree half-rendered - which is
        // the state nobody can reason about afterwards.
        await using var stub = new StubControlPlane();
        var tree = AnAirspaceTreeOnDisk.WithAWorkKind();

        var before = Directory
            .EnumerateFiles(tree.FullName, "*", SearchOption.AllDirectories)
            .Select(f => (f, File.GetLastWriteTimeUtc(f)))
            .OrderBy(x => x.f, StringComparer.Ordinal)
            .ToList();

        try
        {
            await Assert.ThrowsAsync<EnvelopeRefusedException>(
                async () => await Build(stub).AirspacePullAsync(tree.FullName));

            var after = Directory
                .EnumerateFiles(tree.FullName, "*", SearchOption.AllDirectories)
                .Select(f => (f, File.GetLastWriteTimeUtc(f)))
                .OrderBy(x => x.f, StringComparer.Ordinal)
                .ToList();

            await Assert.That(after).IsEquivalentTo(before)
                .Because("every file, and its modification time, exactly as it was. A pull "
                       + "that refuses must leave the tree it refused about alone.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
