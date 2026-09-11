using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A refused apply does not read as success at the command line.
/// </summary>
/// <remarks>
/// <para>
/// <b>A REGRESSION, AND IT WAS MINE.</b> Making a per-document refusal a
/// recorded <c>EstateApplied.Refused</c> rather than a thrown exception was
/// right — throwing discarded every document already applied and every name
/// already declared. But <c>ConsoleApply</c> was taught to render it and
/// <c>AppliedText</c>, which is what the TERMINAL prints, was not.
/// </para>
/// <para>
/// <b>So the command line got worse, not better.</b> It keys on
/// <c>Applied.Count == 0</c> and prints <i>"nothing to apply: the working copy
/// matches the airspace"</i> — so an apply refused on its first document now
/// reports a clean working copy. Before the change it threw and printed the
/// door's own words. Measured immediately: <c>gg airspace apply</c> said the
/// copy matched while <c>gg airspace diff</c> listed three documents.
/// </para>
/// <para>
/// <b>The lesson is the one this repository keeps relearning</b> — a field
/// added to a result has as many renderers as the result has surfaces, and the
/// one nobody updated is the one that lies. <c>Declared</c> had the same gap
/// and is covered here too: a name registration that opened a gate is a
/// governance act, and the terminal said nothing about it either.
/// </para>
/// </remarks>
public class ARefusedApplyIsNotSuccessAtTheTerminalTests
{
    private static string Said(EstateApplied applied) =>
        VerbOutput.ToText(new VerbResult.AirspaceApplied(applied));

    [Test]
    public async Task A_refusal_is_never_reported_as_a_matching_working_copy()
    {
        var said = Said(new EstateApplied
        {
            Applied = [],
            Retiring = [],
            Declared = [],
            Refused = new ApplyRefusal
            {
                Name = "score-hal",
                Path = "airspace/work-kinds/score-hal.yaml",
                Diagnosis = "'score-hal' moves repositories, and repositories is root-only.",
                NotTried = ["dev", "root"],
            },
        });

        await Assert.That(said)
            .DoesNotContain("matches the airspace", StringComparison.Ordinal)
            .Because("nothing was applied because the door REFUSED, which is the opposite "
                   + $"of a working copy that matches. Said: {said}");

        await Assert.That(said).Contains("score-hal", StringComparison.Ordinal)
            .Because("which document stopped it, because the next thing somebody does is "
                   + "open that file.");

        await Assert.That(said).Contains("root-only", StringComparison.Ordinal)
            .Because("in the door's own words. Before this was a recorded refusal it was "
                   + "thrown, and the terminal printed exactly this - losing it is the "
                   + "regression.");
    }

    [Test]
    public async Task What_was_not_tried_is_named()
    {
        var said = Said(new EstateApplied
        {
            Applied = [],
            Retiring = [],
            Declared = [],
            Refused = new ApplyRefusal
            {
                Name = "score-hal",
                Path = "airspace/work-kinds/score-hal.yaml",
                Diagnosis = "refused",
                NotTried = ["dev", "root"],
            },
        });

        await Assert.That(said).Contains("dev", StringComparison.Ordinal);
        await Assert.That(said).Contains("root", StringComparison.Ordinal)
            .Because("a changeset stops as a whole, and somebody who is not told which "
                   + "documents were never sent has to work it out from the safe order.");
    }

    [Test]
    public async Task A_gated_declaration_is_reported_at_the_terminal_too()
    {
        // THE SAME GAP, ON THE FIELD BESIDE IT. A registration that opened a
        // gate changed the tenant's shape and waits on a person; the terminal
        // said nothing about it at all.
        var said = Said(new EstateApplied
        {
            Applied = [],
            Retiring = [],
            Declared =
            [
                new NameDeclared
                {
                    Name = "score-hal",
                    Role = "work-kind",
                    Parent = "root",
                    Flight = "GG-88",
                    Awaiting = "platform-owner",
                    Widens = "the names this tenant can reach",
                },
            ],
        });

        await Assert.That(said).Contains("GG-88", StringComparison.Ordinal);
        await Assert.That(said).Contains("platform-owner", StringComparison.Ordinal)
            .Because("a gate with nobody named is a gate a person cannot go and ask about.");

        await Assert.That(said)
            .DoesNotContain("matches the airspace", StringComparison.Ordinal)
            .Because("a name was declared and a flight opened, so the copy plainly does "
                   + $"not match. Said: {said}");
    }

    [Test]
    public async Task A_genuinely_matching_copy_still_says_so()
    {
        // THE POSITIVE CONTROL. The sentence is right when it is right, and
        // losing it would trade one wrong answer for another.
        var said = Said(new EstateApplied
        {
            Applied = [],
            Retiring = [],
            Declared = [],
        });

        await Assert.That(said).Contains("matches the airspace", StringComparison.Ordinal)
            .Because("nothing applied, nothing declared and nothing refused IS a working "
                   + "copy that matches.");
    }
}
