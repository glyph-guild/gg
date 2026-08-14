using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Attempt two measures its change from the commit attempt one pushed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the pushed commit and not the clone base.</b> The rejection is feedback on the
/// work attempt one pushed, and a reason will usually reference it - "the migration is
/// missing a down step" is about a file already on the branch. A manifest measured from
/// the clone base would re-report every path attempt one touched as though attempt two had
/// touched it, which makes the second gate louder than the first about work nobody
/// changed, and leaves the person no way to see what actually moved.
/// </para>
/// <para>
/// <b>Starting over is a different act and stays available.</b> Ground the flight and fly
/// a new one: a new branch from the pinned base, with no prior attempt to continue from.
/// </para>
/// <para>
/// <b>Safe because the control plane unions manifests.</b> Obligations are evaluated over
/// every manifest a flight has shipped, so an incremental one narrows what a single fact
/// says without narrowing what is measured. Read one at a time, this basis would let a
/// violation introduced in attempt one pass unnoticed in attempt two.
/// </para>
/// </remarks>
public class PriorAttemptBasisTests
{
    [Test]
    public async Task Something_produces_the_prior_attempt_basis()
    {
        // SIXTH INSTANCE OF REGISTERED IS NOT INVOKED, and the one this step closes. The
        // value has been in the contract and in both ledgers since the reject step, and
        // no code path could emit it - a vocabulary entry nothing writes is a fact kind
        // that will be wrong the first time somebody relies on it.
        using var fixture = new AttemptFixture();

        var manifest = fixture.SecondAttemptManifest(
            firstAttempt: ("src/greet.py", "print('hello')\n"),
            secondAttempt: ("src/farewell.py", "print('bye')\n"));

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.DiffBasis).IsEqualTo(DiffBasis.PriorAttempt)
            .Because("the base this was measured from is the previous attempt's head, and a "
                   + "manifest that said two-point would be a number whose meaning depends "
                   + "on history the reader was not given.");
    }

    [Test]
    public async Task It_reports_what_this_attempt_changed_and_not_what_the_last_one_did()
    {
        // The property the basis exists for, and the one a label alone would not give.
        using var fixture = new AttemptFixture();

        var manifest = fixture.SecondAttemptManifest(
            firstAttempt: ("src/greet.py", "print('hello')\n"),
            secondAttempt: ("src/farewell.py", "print('bye')\n"));

        var paths = manifest!.Paths.Select(p => p.Path).ToList();

        await Assert.That(paths).Contains("src/farewell.py")
            .Because("what attempt two did.");
        await Assert.That(paths).DoesNotContain("src/greet.py")
            .Because("and not what attempt one did, which is already on the branch and already "
                   + "in a manifest somebody was shown.");
    }

    [Test]
    public async Task The_base_it_names_is_the_commit_that_was_pushed()
    {
        // The label and the base must not be able to disagree. A manifest claiming the
        // prior-attempt basis while measuring from somewhere else is worse than one
        // claiming two-point, because it reads as more precise.
        using var fixture = new AttemptFixture();

        var manifest = fixture.SecondAttemptManifest(
            firstAttempt: ("src/greet.py", "print('hello')\n"),
            secondAttempt: ("src/farewell.py", "print('bye')\n"));

        await Assert.That(manifest!.BaseCommit).IsEqualTo(fixture.FirstAttemptCommit)
            .Because("the commit destination.pushed recorded for attempt one is the commit "
                   + "this manifest is measured from.");
    }

    [Test]
    public async Task A_first_attempt_still_measures_from_the_clone_base()
    {
        // The control. A flight with no prior attempt is unchanged by any of this, and a
        // basis that quietly applied to every flight would relabel every manifest already
        // recorded.
        using var fixture = new AttemptFixture();

        var manifest = fixture.FirstAttemptManifest(("src/greet.py", "print('hello')\n"));

        await Assert.That(manifest!.DiffBasis).IsEqualTo(DiffBasis.TwoPoint)
            .Because("nothing came before it, so there is nothing else to measure from.");
    }

    [Test]
    public async Task The_lease_is_what_says_where_the_last_attempt_left_off()
    {
        // STRUCTURAL. The runner does not go looking for a previous attempt - it is told,
        // by the only authority on what this flight has already done. A runner that
        // inferred it from a branch it found on the remote would be deciding what work to
        // build on from something anybody with push access can move.
        var members = typeof(LeaseRepoRef).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains(nameof(LeaseRepoRef.ContinuesFrom))
            .Because("Article IX: the client is not an authority on what came before.");
    }
}
