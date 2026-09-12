using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The tab bar's membership follows the offered set, which can grow while the
/// console is running.
/// </summary>
/// <remarks>
/// <para>
/// <b>A CRASH, DIAGNOSED BY ANOTHER SESSION AND VERIFIED HERE.</b> Switching
/// to the Allowances tab threw
/// <c>InvalidOperationException: FocusChanging was not cancelled and the
/// HasFocus value did not change</c> - Terminal.Gui refusing a selection of a
/// pane its <c>Tabs</c> never received.
/// </para>
/// <para>
/// <b>The bar is built ONCE and the selection is recomputed EVERY FRAME.</b>
/// The constructor adds the panes <c>Tabs.Offered</c> allows at that moment;
/// <c>Render</c> asks again and selects out of the answer. Allowances is the
/// only conditional tab - <c>IsAdmin &amp;&amp; FleetAllowancesShown</c> - and
/// <c>IsAdmin</c> arrives from a read that is allowed to fail, so the console
/// still opens against an unreachable control plane. A later refresh that
/// folds a good identity flips it, and the offered set GROWS under a bar that
/// cannot have the pane.
/// </para>
/// <para>
/// <b>The comment above the assignment already named the hazard</b> - <i>"selecting
/// a pane the bar never received would throw"</i> - and guarded only the set
/// SHRINKING. Until the grant shipped, nothing could set the flag, so the tab
/// had never been offered to anybody and the growing case had never run.
/// </para>
/// <para>
/// <b>Pinned where it can be pinned.</b> Nothing in this suite constructs a
/// <c>ConsoleScreen</c> - it needs a terminal, which is why this defect
/// reached a person rather than a test - so the premise is asserted over the
/// pure function and the shape that would have caught it is asserted over the
/// source.
/// </para>
/// </remarks>
public class TheBarFollowsWhatIsOfferedTests
{
    [Test]
    public async Task The_offered_set_grows_when_an_identity_arrives()
    {
        // THE PREMISE THE WHOLE DEFECT RESTS ON. A boot whose whoami failed,
        // or answered a non-administrator, has no Allowances tab; a later
        // refresh can give it one.
        var booted = new AppState { FleetAllowancesShown = true };

        await Assert.That(Tabs.Offered(booted)).DoesNotContain(TabId.Allowances)
            .Because("IsAdmin is false until whoami says otherwise, and the read that "
                   + "answers it is allowed to fail on purpose.");

        var identified = booted with { IsAdmin = true };

        await Assert.That(Tabs.Offered(identified)).Contains(TabId.Allowances)
            .Because("and this is the transition nothing was watching for: the set the bar "
                   + "was built from is not the set the next frame selects out of.");
    }

    [Test]
    public async Task The_screen_reconciles_the_bar_every_frame()
    {
        // THE SHAPE THAT WOULD HAVE CAUGHT IT. A ConsoleScreen cannot be
        // constructed without a terminal, so this reads the source for the
        // property instead: the bar's membership is reconciled where the frame
        // is drawn, rather than only where the screen is built.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var render = screen[screen.IndexOf(
            "    private void Render()", StringComparison.Ordinal)..];

        await Assert.That(render).Contains("FollowTheOffered()", StringComparison.Ordinal)
            .Because("a bar whose membership is decided once cannot show a tab that becomes "
                   + "offered later - and selecting into it throws rather than showing "
                   + "nothing, which is how this arrived as a crash.");

        await Assert.That(screen).Contains("_bar.InsertTab(", StringComparison.Ordinal)
            .Because("a tab that arrives late belongs where it is declared, not appended "
                   + "past the tabs that were there first - the bar's order and Tabs.Next's "
                   + "walk have been made to agree once already.");

        await Assert.That(screen).Contains("_bar.Remove(", StringComparison.Ordinal)
            .Because("following the offered set means shrinking too: a tab that stops being "
                   + "offered has to leave the bar, or it is a door onto a pane the model "
                   + "says is not there.");
    }

    [Test]
    public async Task The_selection_comes_from_what_the_bar_holds()
    {
        // THE SAFETY NET, AND IT IS SEPARATE FROM THE FIX. Reconciling makes
        // the offered pane present; choosing out of what the bar actually
        // holds is what stops a future conditional tab reintroducing the same
        // crash by a different route. The model is what disagreed with the
        // bar, so the model is not what the assignment may consult.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_onTheBar", StringComparison.Ordinal)
            .Because("what the bar holds is recorded rather than inferred.");

        var chosen = screen.IndexOf("var showing = ", StringComparison.Ordinal);
        var assigned = screen.IndexOf("_bar.Value = showing", StringComparison.Ordinal);

        await Assert.That(chosen).IsGreaterThan(0);
        await Assert.That(assigned).IsGreaterThan(chosen);

        await Assert.That(screen[chosen..assigned])
            .DoesNotContain("Tabs.Offered", StringComparison.Ordinal)
            .Because("asking Tabs.Offered at the assignment is asking the model what the "
                   + "widget holds, which is the confusion this whole class is about.");
    }
}
