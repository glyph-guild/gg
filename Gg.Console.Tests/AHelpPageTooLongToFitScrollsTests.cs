using System.Collections.ObjectModel;
using Gg.Console.Views;
using Gg.Local;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// The help pages that are text are scrolled, not truncated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported in one press: "the environment textbox is not scrollable".</b>
/// The Keys page scrolls because a <c>TreeView</c> brings its own scrollbar;
/// Environment and Doctor were <c>Label</c>s, which draw what fits in the box
/// and drop the rest with no mark. This machine lists a dozen variables at four
/// lines each, so most of that page could not be read at all.
/// </para>
/// <para>
/// <b>A list of lines is the console's existing answer</b> — the reading pane,
/// the runner's log and the airspace document are all long read-only text and
/// all three are a <c>ListView</c> of lines wrapped to the viewport. The lines
/// are the model's, which is what lets a test ask what a page says without a
/// terminal.
/// </para>
/// <para>
/// <b>And the refill has to be guarded or the scrollbar is useless.</b>
/// <c>Render</c> runs once a second for the countdown; setting a list's source
/// sends it back to the top, so a page refilled on every render cannot be
/// scrolled for longer than a second. That is not a hypothetical — it is what
/// the tree did on the first cut of this modal, reported as "after one second
/// it snaps the focus to the top".
/// </para>
/// </remarks>
public class AHelpPageTooLongToFitScrollsTests
{
    private static AppState AMachine() => new()
    {
        Settings =
        [
            new EnvironmentSetting
            {
                Name = "GG_CONTROL_PLANE",
                Value = "https://gg.example.test",
                Why = "Which control plane this machine talks to, and the one every "
                    + "other answer on this page is relative to.",
                Source = SettingSources.Environment,
            },
            new EnvironmentSetting
            {
                Name = "EDITOR",
                Value = null,
                Why = "The editor gg hands the terminal to when an intent is written.",
                Source = SettingSources.Unset,
            },
        ],
    };

    [Test]
    public async Task An_environment_page_is_lines_broken_to_the_box()
    {
        var lines = PaneText.HelpPageLines(AMachine(), HelpPage.Environment, columns: 40);

        await Assert.That(lines.Count).IsGreaterThan(1)
            .Because("a page that is one long string is a page a list cannot scroll, "
                   + "which is the Label this replaces.");

        foreach (var line in lines)
        {
            await Assert.That(line.Length).IsLessThanOrEqualTo(40)
                .Because("a ListView clips a line that is wider than it is, and the "
                       + "sentence explaining a variable is the half that gets clipped.");
        }

        await Assert.That(lines.Any(line => line.Contains("GG_CONTROL_PLANE", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the page still says what it always said; only the widget changed.");
    }

    [Test]
    public async Task A_doctor_page_is_lines_too()
    {
        var lines = PaneText.HelpPageLines(AMachine(), HelpPage.Doctor, columns: 40);

        await Assert.That(lines.Count).IsGreaterThan(1);
        await Assert.That(lines.Any(line => line.Contains("gg", StringComparison.Ordinal))).IsTrue()
            .Because("the versions come first on that page and they are the part a "
                   + "person is asked to read back.");
    }

    [Test]
    public async Task A_box_that_has_not_been_laid_out_yet_still_says_something()
    {
        // THE FIRST RENDER. Views are filled before Terminal.Gui has given them
        // a size, so the width is zero exactly once per page - and a wrap at
        // zero that returned nothing would make the page look empty in the one
        // moment somebody is looking at it.
        var lines = PaneText.HelpPageLines(AMachine(), HelpPage.Environment, columns: 0);

        await Assert.That(lines).IsNotEmpty()
            .Because("no width yet is not the same as nothing to say.");
    }

    [Test]
    public async Task Setting_a_list_source_sends_it_back_to_the_top()
    {
        // THE ANCHOR, and the fact the guard below stands on. It is
        // Terminal.Gui's rather than ours, so a version that changes it should
        // fail here rather than in somebody's hands.
        var list = CollectionViews.List();
        list.Height = 4;
        list.SetSource(new ObservableCollection<string>(
            Enumerable.Range(0, 40).Select(i => $"line {i}").ToList()));

        list.SelectedItem = 30;

        await Assert.That(list.SelectedItem).IsEqualTo(30);

        list.SetSource(new ObservableCollection<string>(
            Enumerable.Range(0, 40).Select(i => $"line {i}").ToList()));

        await Assert.That(list.SelectedItem).IsEqualTo(0)
            .Because("identical lines, and the cursor is still lost - so a page refilled "
                   + "once a second can be scrolled for at most a second.");
    }

    [Test]
    public async Task So_the_screen_refills_a_page_only_when_it_changes()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");
        var from = screen.IndexOf("private void FillHelpPages(", StringComparison.Ordinal);

        await Assert.That(from).IsGreaterThan(-1)
            .Because("the fill is named so this guard can read it rather than the "
                   + "whole screen.");

        var body = screen[from..(from + 1400)];

        await Assert.That(body).Contains("SequenceEqual")
            .Because("the anchor above says an unguarded refill loses the scroll, and "
                   + "the three panes that already hold long text all guard it in "
                   + "exactly these words.");
    }

    [Test]
    public async Task And_the_pages_are_lists_rather_than_labels()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).DoesNotContain("private readonly Label _helpEnvironment")
            .Because("a Label is the defect: it draws what fits and says nothing about "
                   + "the rest.");

        await Assert.That(screen).Contains("private readonly ListView _helpEnvironment");
        await Assert.That(screen).Contains("private readonly ListView _helpDoctor");
    }
}
