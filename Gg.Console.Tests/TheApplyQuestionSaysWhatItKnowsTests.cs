using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The apply question says what it knows, and does not claim the working copy
/// matches when nobody could compare it.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT TOLD ONE LIE FOR THREE DIFFERENT FACTS.</b> The question read
/// <c>state.Estate?.Working?.Changes ?? []</c>, so a null diff — no session, a
/// refused read, a control plane that is not up — arrived as two empty lists
/// and was answered with <i>"Nothing to apply: the working copy matches the
/// airspace."</i> That is the one thing it does not mean. A person pressing `s`
/// on a machine with no session was told there was nothing to do about a tree
/// full of documents nobody had compared, and then — if they answered yes
/// anyway — got a refusal, which is the console contradicting itself one
/// keypress apart.
/// </para>
/// <para>
/// <b>The same distinction the changeset modal already makes.</b>
/// <c>PaneText.ChangesetLines</c> keeps the three absences three sentences
/// because only one of them is a thing to go and fix; this is the question
/// beside it, and it had collapsed them. Showing the weaker answer as though
/// it were the stronger one is the shape this console keeps finding.
/// </para>
/// <para>
/// <b>And one unreadable file refuses the whole apply, which the question never
/// mentioned.</b> Apply reads the tree before it sends anything and throws on
/// one of these rather than landing the rest — so a question that lists the
/// changes and omits the refusal describes an apply that cannot happen.
/// </para>
/// </remarks>
public class TheApplyQuestionSaysWhatItKnowsTests
{
    private static AppState Asking(EstateDiff? working, string? diagnosis = null) => new()
    {
        Mode = UiMode.ConfirmApply,
        ActiveTab = TabId.Envelope,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",
            Uncommitted = [],
            Working = working,
            Diagnosis = diagnosis,
            Tree = new WorkingCopy
            {
                Present = true,
                Documents =
                [
                    new("root", "root", "airspace/root.yaml", "v6"),
                    new("work-kind", "score-hal", "airspace/work-kinds/score-hal.yaml", null),
                ],
                Unreadable = [],
            },
        },
    };

    private static EstateDiff Nothing() =>
        new() { Changes = [], Retiring = [], Unreadable = [] };

    [Test]
    public async Task A_diff_nobody_could_ask_for_is_not_a_working_copy_that_matches()
    {
        // THE ONE A PERSON HIT. No control plane, so the estate read answered
        // with a diagnosis and no diff - and the question said the tree was
        // already applied.
        var said = PaneText.Modal(Asking(
            null,
            diagnosis: "Could not reach the control plane at http://localhost:5199: "
                     + "Connection refused (localhost:5199). Try gg doctor."));

        await Assert.That(said).DoesNotContain("matches", StringComparison.OrdinalIgnoreCase)
            .Because("a diff nobody could ask for says nothing about whether the working "
                   + "copy matches, and claiming it does is the console asserting the one "
                   + "fact it has no way to know. Said: " + said);

        await Assert.That(said).Contains("Could not reach the control plane",
                StringComparison.Ordinal)
            .Because("the refusal is the answer, and it carries its own remedy. Said: "
                   + said);
    }

    [Test]
    public async Task Not_compared_yet_is_its_own_answer_too()
    {
        var said = PaneText.Modal(Asking(null));

        await Assert.That(said).DoesNotContain("matches", StringComparison.OrdinalIgnoreCase);

        await Assert.That(said).Contains("not", StringComparison.OrdinalIgnoreCase)
            .Because("nothing asked yet and asked-and-refused are two facts with two "
                   + "different next moves, which is the distinction ChangesetLines makes "
                   + "and this question has to keep. Said: " + said);
    }

    [Test]
    public async Task A_working_copy_that_really_matches_still_says_so()
    {
        // THE TRUE CASE, and it must survive the fix. A diff that was asked
        // for and came back empty IS a working copy that matches, and that is
        // worth saying in those words.
        var said = PaneText.Modal(Asking(Nothing()));

        await Assert.That(said).Contains("matches", StringComparison.OrdinalIgnoreCase)
            .Because("asked and empty is the answer the sentence was written for. Said: "
                   + said);
    }

    [Test]
    public async Task An_unreadable_file_is_named_in_the_question_that_would_hit_it()
    {
        var said = PaneText.Modal(Asking(new EstateDiff
        {
            Changes =
            [
                new DocumentChange
                {
                    Name = "implement",
                    Path = "airspace/work-kinds/implement.yaml",
                    Direction = Changeset.Tightening,
                },
            ],
            Retiring = [],
            Unreadable = ["airspace/narrowings/broken.yaml"],
        }));

        await Assert.That(said).Contains("broken.yaml", StringComparison.Ordinal)
            .Because("apply reads the tree before it sends anything and throws on one of "
                   + "these rather than landing the rest, so answering yes cannot succeed "
                   + "and the question has to say which file. Said: " + said);

        await Assert.That(said).Contains("every", StringComparison.OrdinalIgnoreCase)
            .Because("and that it stops the whole changeset rather than one document, "
                   + "because the rest is part of something somebody meant as a whole.");
    }

    [Test]
    public async Task What_would_actually_happen_is_still_listed_per_document()
    {
        // THE HALF THAT WAS ALWAYS RIGHT. Widenings do not land, and somebody
        // who expected a version would go looking for one that was never
        // minted.
        var said = PaneText.Modal(Asking(new EstateDiff
        {
            Changes =
            [
                new DocumentChange
                {
                    Name = "implement",
                    Path = "airspace/work-kinds/implement.yaml",
                    Direction = Changeset.Tightening,
                },
                new DocumentChange
                {
                    Name = "pci",
                    Path = "airspace/narrowings/pci.yaml",
                    Direction = Changeset.Widening,
                    Field = "obligations",
                },
            ],
            Retiring = [],
            Unreadable = [],
        }));

        await Assert.That(said).Contains("implement", StringComparison.Ordinal);
        await Assert.That(said).Contains("lands", StringComparison.Ordinal);
        await Assert.That(said).Contains("pci", StringComparison.Ordinal);
        await Assert.That(said).Contains("gate", StringComparison.Ordinal)
            .Because("a widening opens a flight and waits, which is the outcome a person "
                   + "most needs told apart from landing. Said: " + said);
    }
}
