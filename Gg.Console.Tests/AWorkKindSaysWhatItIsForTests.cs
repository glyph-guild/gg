using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Choosing what a flight is for, from a table that says what each kind does.
/// </summary>
/// <remarks>
/// <para>
/// <b>A list of bare names is a question only its author can answer.</b> The
/// kinds are a tenant's own words — <c>score-hal</c>, <c>triage</c>,
/// <c>implement</c> — and somebody opening this modal for the first time is
/// being asked to pick between strings they have never seen. A short
/// description beside each is the difference between a choice and a guess.
/// </para>
/// <para>
/// <b>It comes from the envelope, because that is where the kind is
/// defined.</b> A description set somewhere else is one that drifts from what
/// the kind actually does; in the document it lives with the obligations and
/// the loops it describes, in version control, changed by whoever changes them.
/// </para>
/// <para>
/// <b>And a table, for the reason the filter is one.</b> A label with a caret
/// cannot be scrolled or clicked, and two columns of prose in a label is
/// alignment by hand.
/// </para>
/// </remarks>
public class AWorkKindSaysWhatItIsForTests
{
    private static AppState Declaring() => new()
    {
        Mode = UiMode.WorkKindChoice,
        Estate = new EstateOnThisMachine
        {
            Uncommitted = [],
            Names = new EnvelopeTopology
            {
                Names =
                [
                    new TopologyName
                    {
                        Name = "root", Role = Roles.Root, Parent = null,
                        DeclaredBy = "the floor", DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                    new TopologyName
                    {
                        Name = "score-hal", Role = Roles.WorkKind, Parent = "root",
                        Description = "Scores backlog items against the HAL rubric.",
                        DeclaredBy = "Kevin", DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                    new TopologyName
                    {
                        Name = "implement", Role = Roles.WorkKind, Parent = "root",
                        DeclaredBy = "Kevin", DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
        },
    };

    [Test]
    public async Task A_kind_carries_the_words_its_envelope_gave_it()
    {
        var kinds = WorkKinds.Declared(Declaring());

        await Assert.That(kinds.Select(k => k.Name))
            .IsEquivalentTo((string[])["score-hal", "implement"]);

        await Assert.That(kinds.Single(k => k.Name == "score-hal").Description)
            .IsEqualTo("Scores backlog items against the HAL rubric.");

        await Assert.That(kinds.Single(k => k.Name == "implement").Description).IsNull()
            .Because("a kind declared before descriptions existed has none, and inventing "
                   + "one from its name would be this console describing somebody else's "
                   + "governance.");
    }

    [Test]
    public async Task Row_zero_is_still_the_answer_every_flight_used_to_give()
    {
        // `NO KIND' IS A ROW AND IT IS THE FIRST ONE. Inheriting the floor is
        // an answer - it is what every flight before kinds existed was - so it
        // is on the list rather than being what happens if you escape.
        var rows = WorkKinds.Rows(Declaring());

        await Assert.That(rows[0].Name).IsEmpty();
        await Assert.That(rows[0].Said).Contains("floor");

        await Assert.That(WorkKinds.Picked(Declaring() with { KindSelected = 0 })).IsNull()
            .Because("the control plane reads a missing kind as implement, so sending that "
                   + "word would be declaring something nobody chose.");

        await Assert.That(WorkKinds.Picked(Declaring() with { KindSelected = 1 }))
            .IsEqualTo("score-hal");
    }

    [Test]
    public async Task The_choices_are_a_table_with_the_words_beside_the_name()
    {
        await Assert.That(Rows.WorkKindColumns)
            .IsEquivalentTo((string[])["kind", "what it is for"]);
    }

    [Test]
    public async Task The_box_is_a_document_because_the_words_need_the_room()
    {
        await Assert.That(PaneText.ModalIsADocument(UiMode.WorkKindChoice)).IsTrue()
            .Because("a question with two answers wants a box an eye takes in at once; a "
                   + "tenant's kinds with a sentence each want the screen.");
    }

    [Test]
    public async Task The_screen_draws_it_as_a_table()
    {
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("_kindChoices")
            .Because("the widget every other list in this console uses, so it scrolls and "
                   + "can be clicked.");

        await Assert.That(screen).Contains("Rows.WorkKindColumns")
            .Because("the columns are the model's, or nothing can be asked what heading was "
                   + "drawn.");
    }
}
