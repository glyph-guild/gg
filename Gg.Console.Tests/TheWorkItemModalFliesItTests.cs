using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The work item modal has a tab for doing something about it, and the first
/// thing is flying it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reading and acting were one keystroke apart and in the wrong order.</b>
/// <c>f</c> flies what the cursor is on, which means the way to fly an item you
/// have opened is to close the thing you opened to read it and press a key over
/// a row. The modal is where a person decides; it had no way to act.
/// </para>
/// <para>
/// <b>A tab rather than a key, because a key is not a button.</b> The ask was a
/// button, and <see cref="Keymap.Buttons"/> cannot give one: it is all-or-none
/// per mode, so a labelled <c>f</c> beside an unlabelled <c>j</c> and <c>k</c>
/// draws nothing, and labelling the cursor keys would put "down" and "up" in a
/// button row. So the button lives in a pane, which is the shape
/// <c>_runnerStart</c> already uses for the same reason - a thing a person
/// cannot type while the console owns the terminal.
/// </para>
/// <para>
/// <b>Always offered, never conditional.</b> A tab that comes and goes costs a
/// keymap flag and a ratchet, and buys a person the experience of a tab that
/// was there a moment ago. It is always the fourth tab and it says why it
/// cannot fly when it cannot.
/// </para>
/// </remarks>
public class TheWorkItemModalFliesItTests
{
    private static AppState Reading() => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.WorkItemDetail,
        BrowseSelected = 0,
        Browse = new BrowseListing
        {
            ProviderKey = "a-tracker",
            Items =
            [
                new BrowseRow
                {
                    Id = "18515",
                    Title = "Oz asks guided questions",
                    State = "Active",
                },
            ],
        },
    };

    [Test]
    public async Task Acting_is_the_fourth_tab_and_it_is_appended()
    {
        // APPENDED, for the reason the enum states about Fields: the first
        // member is what `default(WorkItemTab)` means and what this modal opens
        // on, so a tab inserted in front silently moves every state that never
        // set one.
        var tabs = Enum.GetValues<WorkItemTab>();

        await Assert.That(tabs).Contains(WorkItemTab.Actions);
        await Assert.That(tabs[^1]).IsEqualTo(WorkItemTab.Actions)
            .Because("Details has to stay the default, and appending is what keeps a persisted "
                   + "value meaning what it meant when it was written.");
    }

    [Test]
    public async Task The_key_that_cycles_the_tabs_reaches_it()
    {
        // A TAB A KEY CANNOT ARRIVE AT IS A TAB NOBODY FINDS - the sentence the
        // third tab was added under, and the fourth is no different.
        var fields = Reading() with { WorkItemTab = WorkItemTab.Fields };
        var actions = Reducer.Reduce(fields, Command.NextWorkItemTab);

        await Assert.That(actions.WorkItemTab).IsEqualTo(WorkItemTab.Actions);

        await Assert.That(Reducer.Reduce(actions, Command.NextWorkItemTab).WorkItemTab)
            .IsEqualTo(WorkItemTab.Details)
            .Because("the cycle wraps, so the last tab is not a place a person reaches and "
                   + "cannot leave by the key that got them there.");
    }

    [Test]
    public async Task The_tab_says_what_flying_this_would_do()
    {
        // AND IT IS THE PANE THAT SAYS IT, not the button's caption. A button
        // reading "Fly this" over an empty pane leaves a person to guess what
        // it opens and against what.
        await Assert.That(WorkItemDetails.CanFly(Reading())).IsTrue();

        await Assert.That(WorkItemDetails.ActionsSaid(Reading()))
            .Contains("18515")
            .Because("the id is what a person checks before starting work on something.");
    }

    [Test]
    public async Task With_nothing_to_fly_it_says_so_rather_than_offering_a_dead_button()
    {
        // Article XI, and the rule the runner button is offered under: a button
        // that appears to work and does nothing is worse than one that is not
        // there.
        var nothing = Reading() with { Browse = null };

        await Assert.That(WorkItemDetails.CanFly(nothing)).IsFalse();
        await Assert.That(WorkItemDetails.ActionsSaid(nothing)).IsNotEmpty()
            .Because("a tab that goes blank claims the tracker said nothing, when what "
                   + "happened is that there is nothing here to fly.");
    }

    [Test]
    public async Task Copying_the_modal_on_this_tab_takes_this_tab()
    {
        // THE RULE PaneText ALREADY KEEPS for the other three: `c` copies what
        // is showing, so a tab with no arm hands over another tab's text.
        var text = PaneText.Modal(Reading() with { WorkItemTab = WorkItemTab.Actions });

        await Assert.That(text).Contains(WorkItemDetails.ActionsTitle);
    }

    [Test]
    public async Task The_fields_tab_is_called_what_it_holds()
    {
        // "what the tracker records" described the CONTENTS and read as a
        // sentence in a tab strip, where every other tab is one or two words.
        // The other three are `what it says', `log' and this - and what this
        // holds is the fields a listing did not carry.
        await Assert.That(WorkItemDetails.FieldsTitle).IsEqualTo("other fields");
    }
}
