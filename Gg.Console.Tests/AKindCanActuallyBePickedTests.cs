using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The cursor in the kinds modal has to land on the kinds.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED AS "I am unable to select a work kind in that modal", and it was
/// exactly that.</b> The choices became a table, and a table moves its own
/// cursor with the arrows and raises <c>ValueChanged</c> — which this console
/// routes through <see cref="Reducer.Pointed"/>. That method answers by MODE
/// first for three modals and then falls through to the active TAB. The kinds
/// modal was not one of the three, so every arrow keypress moved the cursor of
/// the list BEHIND the dialog, <c>KindSelected</c> never changed, and the next
/// render refilled the table from it — putting the highlight back on row zero,
/// forever.
/// </para>
/// <para>
/// <b>The comment warning about this was already in the file, twice.</b> The
/// runner modal's arm says it: <i>"the tab behind this modal is Runners, so
/// falling through would move the FLEET's cursor"</i>. Three modals were given
/// the arm as they became widgets and the fourth was not, which is what a list
/// of special cases does when it is a list rather than a rule.
/// </para>
/// </remarks>
public class AKindCanActuallyBePickedTests
{
    private static AppState Asking() => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.WorkKindChoice,
        BrowseSelected = 3,
        Browse = new BrowseListing
        {
            ProviderKey = "a-tracker",
            Items =
            [
                .. Enumerable.Range(0, 6).Select(n => new BrowseRow
                {
                    Id = $"1{n}", Title = "something", State = "Active",
                }),
            ],
        },
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
                        DeclaredBy = "Kevin", DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                    new TopologyName
                    {
                        Name = "triage", Role = Roles.WorkKind, Parent = "root",
                        DeclaredBy = "Kevin", DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
        },
    };

    [Test]
    public async Task Pointing_at_a_row_picks_the_kind_and_not_the_work_behind_it()
    {
        // WHAT THE TABLE RAISES. Arrows are the widget's own and never reach
        // the keymap; the subscription IS the keyboard for this list, and it
        // hands the row to Pointed.
        var pointed = Reducer.Pointed(Asking(), 2);

        await Assert.That(pointed.KindSelected).IsEqualTo(2)
            .Because("row two of the kinds is `triage' - the modal's own list, which is the "
                   + "only list this dialog is about.");

        await Assert.That(pointed.BrowseSelected).IsEqualTo(3)
            .Because("the work list is BEHIND this modal, and moving it would change which "
                   + "item the flight is for while somebody is deciding what it is for.");
    }

    [Test]
    public async Task The_cursor_survives_the_render_that_follows_it()
    {
        // THE HALF THAT MADE IT LOOK STUCK RATHER THAN WRONG. The table is
        // refilled from the model on every render, so a cursor the model did
        // not learn about is a highlight that snaps back to row zero - which
        // reads as a modal that will not move at all.
        var moved = Reducer.Pointed(Asking(), 1);

        await Assert.That(WorkKinds.Picked(moved)).IsEqualTo("score-hal")
            .Because("what enter opens a flight for is read from KindSelected, so a cursor "
                   + "the model never saw is a choice that cannot be made.");
    }

    [Test]
    public async Task It_clamps_rather_than_trusting_the_row_it_is_handed()
    {
        await Assert.That(Reducer.Pointed(Asking(), 99).KindSelected).IsEqualTo(2)
            .Because("a list that shrank under a cursor - a topology read again with a kind "
                   + "retired - would otherwise pick by index into nothing.");

        await Assert.That(Reducer.Pointed(Asking(), -4).KindSelected).IsEqualTo(0);
    }

    [Test]
    public async Task Every_modal_made_of_widgets_answers_before_the_tab_does()
    {
        // THE RULE, RATHER THAN A FOURTH SPECIAL CASE. Pointed answers by mode
        // for every mode whose modal holds a list of its own; the arm being
        // missing for one of them is how this shipped.
        foreach (var mode in (UiMode[])
            [UiMode.FlightDetail, UiMode.Runner, UiMode.BrowseFilter,
             UiMode.WorkItemDetail, UiMode.WorkKindChoice])
        {
            var behind = Asking() with { Mode = mode };

            await Assert.That(Reducer.Pointed(behind, 1).BrowseSelected)
                .IsEqualTo(behind.BrowseSelected)
                .Because($"{mode} draws a list of its own, so a row pointed at inside it must "
                       + "never reach the tab underneath.");
        }
    }
}
