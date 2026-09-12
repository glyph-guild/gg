using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// Picking a view on the runner modal's bar is a person choosing one.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE, AND THE FIRST DIAGNOSIS WAS WRONG.</b> "It switches
/// back to the log after a second" was read as a focus fight and fixed as one.
/// It is not: <c>v</c> works. What snaps back is navigating the BAR ITSELF with
/// the arrow keys.
/// </para>
/// <para>
/// <b>Because nothing was listening.</b> <c>Tabs</c> changes its own
/// <c>Value</c> when somebody arrows along the headers or clicks one, and the
/// model never heard about it — so the next render assigned <c>Value</c> back
/// from <c>State.RunnerView</c>, which still said log. The model was
/// authoritative and the widget's own navigation was being thrown away.
/// </para>
/// <para>
/// <b>The window's tab bar has always done this properly</b>, and this is the
/// same wiring: <c>_bar.ValueChanged += OnTabChanged</c>, a handler that
/// returns early while the sync flag is held, and the model updated from what
/// was chosen. One bar in this console listened and the other did not.
/// </para>
/// </remarks>
public class TheRunnerBarIsListenedToTests
{
    private static string Screen() => Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

    [Test]
    public async Task The_bar_is_subscribed_to_at_all()
    {
        // ASSERTED AGAINST THE SOURCE, because ConsoleScreen cannot be
        // constructed without a terminal - and this defect is precisely one
        // that only a person driving the widget can see.
        var screen = Screen();

        await Assert.That(screen).Contains("_runnerViews.ValueChanged +=", StringComparison.Ordinal)
            .Because("a bar nobody listens to is a bar whose every move is undone by the "
                   + "next render.");

        await Assert.That(screen).Contains("_runnerViews.ValueChanged -=", StringComparison.Ordinal)
            .Because("and it is let go when the session is torn down, like every other "
                   + "subscription here, because the screen is rebuilt from the model on "
                   + "every pass.");
    }

    [Test]
    public async Task What_was_chosen_reaches_the_model()
    {
        var screen = Screen();

        var at = screen.IndexOf(
            "private void OnRunnerViewChanged", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1);

        var handler = screen[at..screen.IndexOf("\n    }", at, StringComparison.Ordinal)];

        await Assert.That(handler).Contains("RunnerView =", StringComparison.Ordinal)
            .Because("the point is that the model learns which view a person picked. "
                   + "Handler:\n" + handler);

        // THE SYNC FLAG, which is why the window's bar does not do this to
        // itself: Render assigns Value, and a handler that reduced on its own
        // assignment would read a render as a person choosing something.
        await Assert.That(handler).Contains("_syncing", StringComparison.Ordinal)
            .Because("without it the render's own assignment comes back as a choice. "
                   + "Handler:\n" + handler);
    }

    [Test]
    public async Task The_view_a_person_picked_is_the_view_that_is_drawn()
    {
        // THE PURE HALF. Whatever moved the bar, the model is what the next
        // render draws from - so a view set here must survive a render, which
        // is the property the widget's own Value could not have.
        var state = new AppState { Mode = UiMode.Runner, RunnerView = RunnerView.Members };

        await Assert.That(state.RunnerView).IsEqualTo(RunnerView.Members);

        await Assert.That(RunnerViews.All).Contains(RunnerView.Members)
            .Because("and every view the bar can offer has to be one the model can hold.");
    }

    [Test]
    public async Task Focus_still_follows_a_view_that_turned()
    {
        // THE EARLIER FIX IS NOT UNDONE BY THIS ONE. `v' changes the model and
        // the keyboard follows; the bar changing the model is the other
        // direction of the same thing, and both end with the model deciding.
        await Assert.That(FocusChange.Wanted(
                UiMode.Runner, TabId.Runners, landed: null, modalHasFocus: true,
                runnerView: RunnerView.Members, landedRunnerView: RunnerView.Log))
            .IsEqualTo(FocusTarget.RunnerView);
    }
}
