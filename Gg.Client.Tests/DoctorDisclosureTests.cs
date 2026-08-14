using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A permanent disclosure is not a failure, and must not look like one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The moves check is non-blocking, not fixable, and never passing.</b> That combination
/// is normally a defect - a check that can never go green is a check somebody will learn to
/// scroll past, and <b>a flake that gets ignored trains people to ignore red</b>. This one
/// is not a flake: it is a true statement about the product that will keep being true until
/// moves are enforced, and it is reported every time precisely so nobody assumes an
/// envelope's <c>moves</c> list bounds what an agent may do.
/// </para>
/// <para>
/// <b>So it needs to render as its own thing.</b> Two booleans described two states and
/// there are three; the third has to look like a third rather than like the failure it sits
/// next to, or it inherits that failure's ability to be ignored.
/// </para>
/// </remarks>
public class DoctorDisclosureTests
{
    [Test]
    public async Task A_check_is_one_of_three_states()
    {
        // Not two booleans that can contradict each other. A check claiming to have both
        // passed and disclosed is a state nobody can render, and picking one for it
        // silently is how the wrong one gets shown.
        var report = await RunAsync();

        foreach (var check in report.Checks)
        {
            await Assert.That(Enum.IsDefined(check.Outcome)).IsTrue()
                .Because($"'{check.Name}' is in state '{check.Outcome}'.");
        }
    }

    [Test]
    public async Task The_moves_disclosure_is_a_disclosure_rather_than_a_failure()
    {
        var moves = (await RunAsync()).Checks.Single(c => c.Name == DoctorChecks.Moves);

        await Assert.That(moves.Outcome).IsEqualTo(DoctorOutcome.Disclosure);
        await Assert.That(moves.Passed).IsFalse()
            .Because("it has not passed - what it found is that a bound somebody expects is "
                   + "absent - and saying it passed would be the lie the other direction.");
    }

    [Test]
    public async Task It_renders_differently_from_a_failure()
    {
        // THE POINT. A flag nothing looks at is not a third state; what makes it one is
        // that a person reading the output can tell it apart without knowing the rules.
        var rendered = VerbOutput.ToText(new VerbResult.Diagnosis(await RunAsync()));

        var movesLine = rendered.Split('\n')
            .Single(l => l.Contains(DoctorChecks.Moves, StringComparison.Ordinal));

        await Assert.That(movesLine).DoesNotContain("warn")
            .Because("a permanent disclosure marked the same as a real non-blocking failure "
                   + "is a line somebody learns to scroll past, which is exactly what makes "
                   + "the failures beside it easier to ignore too.");
        await Assert.That(movesLine).DoesNotContain("STOP");
        await Assert.That(movesLine).DoesNotContain("ok");

        await Assert.That(movesLine).Contains("note")
            .Because("and it has its own mark, so the reader learns three things rather than "
                   + "two and an exception.");
    }

    [Test]
    public async Task A_real_non_blocking_failure_still_says_warn()
    {
        // ASK WHY IT PASSES: if every check rendered as a note, the assertion above would
        // hold and the distinction would have been abolished rather than drawn.
        var rendered = VerbOutput.ToText(new VerbResult.Diagnosis(await RunAsync()));

        var others = rendered.Split('\n')
            .Where(l => !l.Contains(DoctorChecks.Moves, StringComparison.Ordinal))
            .ToList();

        await Assert.That(others.Any(l => l.Contains("warn", StringComparison.Ordinal)
                                       || l.Contains("ok", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the other checks still use the marks they always did, so this drew a "
                   + "distinction rather than abolishing one.");
    }

    [Test]
    public async Task The_disclosure_says_it_will_not_change()
    {
        // What separates it from a warning in words as well as in a mark: a warning is
        // asking for something, and this is telling you how the product works.
        var moves = (await RunAsync()).Checks.Single(c => c.Name == DoctorChecks.Moves);

        await Assert.That(moves.Fixable).IsFalse();
        await Assert.That(moves.Blocking).IsFalse();
        await Assert.That(moves.Detail).Contains("not enforced")
            .Because("it states the property rather than requesting an action.");
    }


    // ---- the three properties that keep the category honest ----

    [Test]
    public async Task A_disclosure_does_not_count_as_a_failing_check()
    {
        // CRY WOLF, IN THE TOOL PEOPLE RUN WHEN SOMETHING IS ALREADY WRONG. A doctor
        // reporting "3 checks failed" when one of them can never pass teaches somebody
        // that the number is inflated, and the next number they discount is a real one.
        var report = await RunAsync();

        await Assert.That(report.Failed)
            .IsEqualTo(report.Checks.Count(c => c.Outcome == DoctorOutcome.Fail))
            .Because("the count is of things that failed, and a disclosure did not fail.");

        await Assert.That(report.Checks.Any(c => c.Outcome == DoctorOutcome.Disclosure)).IsTrue()
            .Because("ASK WHY IT PASSES: with no disclosure present, excluding them changes "
                   + "nothing and this asserts an empty difference.");
    }

    [Test]
    public async Task A_disclosure_does_not_change_the_exit_status()
    {
        // The scripted half of the same property. A doctor that exits non-zero forever is
        // one people stop running, and then it is not there on the day it matters.
        var report = await RunAsync();

        await Assert.That(report.ExitCode).IsEqualTo(0)
            .Because("nothing blocking failed. The standing disclosure is not a reason for "
                   + "a script to stop.");
    }

    [Test]
    public async Task Disclosures_are_rendered_in_their_own_section()
    {
        // RENDERS APART, which is stronger than rendering differently. A line inside a
        // list of checks reads as an item that is not passing however it is marked - and
        // separating them is what lets the checks list be genuinely all-green, which is
        // what makes an all-green run mean something.
        var rendered = VerbOutput.ToText(new VerbResult.Diagnosis(await RunAsync()));

        var lines = rendered.Split('\n').ToList();
        var checksEnd = lines.FindIndex(l => l.StartsWith("also true", StringComparison.Ordinal));

        await Assert.That(checksEnd).IsGreaterThan(0)
            .Because("there is a section boundary at all.");

        var checksList = lines.Take(checksEnd).ToList();

        await Assert.That(checksList.Any(l => l.Contains(DoctorChecks.Moves, StringComparison.Ordinal)))
            .IsFalse()
            .Because("the disclosure is not among the checks, so a run with nothing wrong "
                   + "shows a checks list with nothing wrong in it.");

        await Assert.That(lines.Skip(checksEnd).Any(l => l.Contains(DoctorChecks.Moves, StringComparison.Ordinal)))
            .IsTrue()
            .Because("and it is still reported, every time, which is the whole reason it "
                   + "exists.");
    }

    [Test]
    public async Task A_disclosure_carries_no_remedy_and_that_is_what_makes_it_one()
    {
        // THE DISCRIMINATOR. The obvious abuse of a third category is reclassifying an
        // inconvenient failing check as a disclosure, and what stops it is that a
        // disclosure is a check with NO ACTION THE USER CAN TAKE. One carrying a suggested
        // remedy is a miscategorised check, and this is a structural rule rather than a
        // convention somebody remembers.
        var report = await RunAsync();

        foreach (var disclosure in report.Checks.Where(c => c.Outcome == DoctorOutcome.Disclosure))
        {
            await Assert.That(disclosure.Fixable).IsFalse()
                .Because($"'{disclosure.Name}' discloses, so there is nothing to fix.");
            await Assert.That(disclosure.Fix).IsNull()
                .Because($"'{disclosure.Name}' offers a remedy, which means it is a check "
                       + "that failed and was filed as a fact about the product.");
            await Assert.That(disclosure.Blocking).IsFalse()
                .Because($"'{disclosure.Name}' blocks, and something that stops gg working "
                       + "is not a standing note about how it works.");
        }
    }

    [Test]
    public async Task The_disclosure_says_what_it_means_rather_than_only_what_is_true()
    {
        // A statement of fact leaves the reader to work out the consequence, and the
        // consequence is the reason this is reported at all: somebody reading an
        // envelope's moves list needs to know it records intent rather than bounding
        // anything.
        var moves = (await RunAsync()).Checks.Single(c => c.Name == DoctorChecks.Moves);

        await Assert.That(moves.Detail).Contains("record of intent")
            .Because("what it means for the person reading the envelope, not only what is "
                   + "true of the executor.");
    }

    /// <summary>The doctor as `gg doctor` runs it, against a stubbed control plane.</summary>
    private static async Task<DoctorReport> RunAsync()
    {
        await using var stub = new StubControlPlane();

        var doctor = new Doctor(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(DoctorTests.AValidSession()),
            DoctorTests.ScratchStore(),
            new Uri(stub.BaseAddress));

        return await doctor.RunAsync();
    }
}