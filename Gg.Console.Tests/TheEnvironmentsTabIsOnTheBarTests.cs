using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The Environments tab: what a person can see about the environments this
/// tenant has charted.
/// </summary>
/// <remarks>
/// <para>
/// <b>UNCONDITIONAL, unlike the fleet's allowances.</b> A tenant that has
/// charted nothing gets a tab that says so, which is how anybody learns the
/// chart exists at all — and an envelope naming an uncharted environment is
/// refused pointing at it. A conditional tab would hide the feature from
/// exactly the people the refusal sends here.
/// </para>
/// <para>
/// <b>It is also the cheaper shape, and that is not a coincidence.</b>
/// <c>Tabs.Offered</c>'s conditional path is what crashed the console twice
/// when the allowances tab arrived: once because the bar's membership was
/// frozen at construction, once because the landing switch had a default that
/// answered. A tab that is always offered walks neither.
/// </para>
/// </remarks>
public class TheEnvironmentsTabIsOnTheBarTests
{
    private static AppState Bare() => new();

    [Test]
    public async Task It_is_offered_before_anybody_signs_in()
    {
        await Assert.That(Tabs.Offered(Bare())).Contains(TabId.Environments)
            .Because("the chart is what an envelope naming an environment is refused "
                   + "against, so the tab has to be findable by somebody reading that "
                   + "refusal - which is somebody who has not been here before.");
    }

    [Test]
    public async Task The_key_that_reaches_it_is_the_key_the_tab_advertises()
    {
        // `s' BECAUSE OF WHAT IS LEFT. Normal mode has m, s and z free: `p' is
        // refused by name - nothing may answer the key the checklist had - and
        // `z' reads as nothing, which Tabs.KeyFor already says about the
        // allowances tab. `s' is in the word and it is free, which is the
        // argument `u' came in on for Runners.
        await Assert.That(Tabs.KeyFor(TabId.Environments)).IsEqualTo(KeyStroke.Char('s'));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('s'), new KeymapContext()))
            .IsEqualTo(Tabs.CommandFor(TabId.Environments))
            .Because("a tab that advertises a key nothing answers is worse than a tab with "
                   + "no key on it.");
    }

    [Test]
    public async Task Showing_it_asks_for_the_reads_it_is_drawn_from()
    {
        var command = Tabs.CommandFor(TabId.Environments);

        await Assert.That(command).IsNotNull();
        await Assert.That(ShellCommands.Reads).Contains(command!.Value)
            .Because("the chart, the strategies and the ledger are none of them in the model "
                   + "at boot, so a tab that asked for nothing would be permanently empty - "
                   + "and a read that ended the session would take a whole screen away and "
                   + "give it back to fetch three documents.");

        await Assert.That(ShellCommands.Handled).DoesNotContain(command!.Value);
    }

    [Test]
    public async Task An_unread_chart_a_charted_nothing_and_an_unfurnished_name_are_three_sentences()
    {
        // THREE ABSENCES, THREE SENTENCES - PaneText.Estate's rule. One line
        // for all three would lose the difference between never-asked,
        // nothing-charted, and charted-but-nothing-furnishes-it, which are
        // three things a person does differently about.
        var unread = PaneText.ForTab(Bare(), TabId.Environments);

        await Assert.That(unread).Contains("not read", StringComparison.Ordinal)
            .Because("nobody has asked yet, and the remedy is a keystroke. Said: " + unread);

        var charted = PaneText.ForTab(
            Bare() with { Chart = new EnvironmentChart { Environments = [] } },
            TabId.Environments);

        await Assert.That(charted).Contains("charted", StringComparison.Ordinal);
        await Assert.That(charted).DoesNotContain("not read", StringComparison.Ordinal)
            .Because("a tenant that has charted nothing is set up and has said nothing, which "
                   + "is a different fact from one nobody asked about. Said: " + charted);
    }

    [Test]
    public async Task A_read_that_went_and_failed_does_not_say_nobody_asked()
    {
        // FOUND BY RUNNING IT. Against a control plane that was not there the
        // pane said "not read - press s" - on the tab a person had reached BY
        // pressing s. Never-asked and asked-and-failed are the first two of the
        // three absences, and the one thing a reader does about them differs:
        // press the key, or go and find out why the answer never came.
        var asked = Bare() with
        {
            EnvironmentsVisible = true,
            Diagnosis = "Connection refused",
        };

        var said = PaneText.ForTab(asked, TabId.Environments);

        await Assert.That(said).DoesNotContain("not read", StringComparison.Ordinal)
            .Because("the tab is open, which means the request went. Said: " + said);

        await Assert.That(said).Contains("Connection refused", StringComparison.Ordinal)
            .Because("and what came back instead is the only thing worth saying about it.");
    }

    [Test]
    public async Task A_charted_name_is_rows_rather_than_a_sentence()
    {
        var state = Bare() with
        {
            Chart = new EnvironmentChart
            {
                Environments =
                [
                    new EnvironmentCharted
                    {
                        Name = "aspire-payments",
                        Meaning = "ran here",
                        Disposition = LabelDispositions.Measured,
                        ChartedBy = "Kevin",
                        ChartedAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
                    },
                ],
            },
        };

        await Assert.That(PaneText.ForTab(state, TabId.Environments)).IsEmpty()
            .Because("the rows are the answer from here on - the airspace tab's rule, and the "
                   + "reason a pane and a table do not both try to say the same thing.");

        await Assert.That(EnvironmentRows.Environments(state)).IsNotEmpty();
    }

    [Test]
    public async Task The_cursor_on_this_tab_is_its_own()
    {
        // THE DEFAULT THAT ANSWERS, one reducer over: Moved and Pointed both
        // fall through to the QUEUE's cursor, so a tab with no arm of its own
        // moves a selection on a pane nobody is looking at - silently, because
        // nothing throws.
        var state = Bare() with
        {
            ActiveTab = TabId.Environments,
            Chart = new EnvironmentChart
            {
                Environments =
                [
                    new EnvironmentCharted
                    {
                        Name = "a", Disposition = LabelDispositions.Stated,
                        ChartedBy = "Kevin", ChartedAt = DateTimeOffset.UnixEpoch,
                    },
                    new EnvironmentCharted
                    {
                        Name = "b", Disposition = LabelDispositions.Stated,
                        ChartedBy = "Kevin", ChartedAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
        };

        var moved = Reducer.Reduce(state, Command.SelectNext);

        await Assert.That(moved.EnvironmentSelected).IsEqualTo(1);
        await Assert.That(moved.SelectedRow).IsEqualTo(state.SelectedRow)
            .Because("the queue's cursor is on a pane nobody is looking at.");
    }

    [Test]
    public async Task The_name_under_the_cursor_is_what_the_pane_can_say_more_about()
    {
        var state = Bare() with
        {
            Chart = new EnvironmentChart
            {
                Environments =
                [
                    new EnvironmentCharted
                    {
                        Name = "zulu", Disposition = LabelDispositions.Stated,
                        ChartedBy = "Kevin", ChartedAt = DateTimeOffset.UnixEpoch,
                    },
                    new EnvironmentCharted
                    {
                        Name = "alpha", Disposition = LabelDispositions.Stated,
                        ChartedBy = "Kevin", ChartedAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
            EnvironmentSelected = 1,
        };

        await Assert.That(EnvironmentRows.Pointed(state)).IsEqualTo("zulu")
            .Because("the cursor indexes the ROWS' order, which is ordinal by name - so row "
                   + "one is zulu however the chart arrived.");

        await Assert.That(EnvironmentRows.Pointed(Bare())).IsNull();
    }
}
