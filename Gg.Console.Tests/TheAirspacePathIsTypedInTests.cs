using Gg.Console;
using Gg.Console.Views;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The airspace path is typed into a field at the bottom of the tab.
/// </summary>
/// <remarks>
/// <para>
/// <b>Replacing the <c>$EDITOR</c> round trip, which was the wrong shape for a
/// path.</b> Handing a whole terminal to another program to collect one line is
/// ceremony a person pays for every correction, and the thing being corrected
/// is usually a typo.
/// </para>
/// <para>
/// <b>It is a mode, because a field that accepts keystrokes owns the
/// keyboard.</b> This console has measured that hazard once already —
/// <c>TableView</c>'s type-to-search ate all twenty-one keys the moment rows
/// arrived, which is why <c>QuietTable</c> exists — and an always-focused field
/// on this tab would eat <c>p</c>, <c>s</c>, <c>m</c>, <c>j</c> and <c>k</c>.
/// So the field is unfocusable until <c>enter</c> puts the console in its mode,
/// where the only two keys are the two a person needs: <c>enter</c> applies and
/// <c>esc</c> leaves it alone. That is the modal contract every other mode here
/// keeps.
/// </para>
/// <para>
/// <b>And the write still happens between sessions.</b>
/// <c>ConsoleScreen.cs</c> says nothing in this console is written by typing
/// into a widget, <i>"a write happens between sessions with the terminal
/// provably free"</i>. The field collects; <c>enter</c> ends the session; the
/// shell writes the file. What changed is where the text comes from, not when
/// the file moves — so the sentence stays true and is worth keeping.
/// </para>
/// </remarks>
public class TheAirspacePathIsTypedInTests
{
    [Test]
    public async Task Enter_on_the_airspace_tab_opens_the_field()
    {
        await Assert.That(Keymap.Resolve(
                KeyStroke.EnterKey, new KeymapContext(UiMode.Normal, TabId.Envelope)))
            .IsEqualTo(Command.FocusAirspacePath)
            .Because("`enter` is already what a tab means by 'open the thing this tab is "
                   + "about' - the flight on the queue, the runner on the fleet - and on "
                   + "this tab the thing is where the airspace is.");
    }

    [Test]
    public async Task Enter_still_opens_a_flight_everywhere_else()
    {
        // THE CONTROL ON THE KEY. `enter` is tab-scoped already, and taking it
        // for this tab must not take it from the two that had it.
        await Assert.That(Keymap.Resolve(
                KeyStroke.EnterKey, new KeymapContext(UiMode.Normal, TabId.Queue)))
            .IsEqualTo(Command.ShowFlight);

        await Assert.That(Keymap.Resolve(
                KeyStroke.EnterKey, new KeymapContext(UiMode.Normal, TabId.Runners)))
            .IsEqualTo(Command.ShowRunner);
    }

    [Test]
    public async Task Opening_the_field_is_the_session_s_own_work()
    {
        // IT HOLDS NO I/O, which is what lets it happen inside a session while
        // the write it leads to happens outside one - ComposeChoice's property,
        // and the reason a mode can be opened by a reducer at all.
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.FocusAirspacePath);
        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.FocusAirspacePath);

        var opened = Reducer.Reduce(
            new AppState { ActiveTab = TabId.Envelope }, Command.FocusAirspacePath);

        await Assert.That(opened.Mode).IsEqualTo(UiMode.AirspacePath);
    }

    [Test]
    public async Task The_field_takes_only_enter_and_escape()
    {
        var inside = new KeymapContext(UiMode.AirspacePath, TabId.Envelope);

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, inside))
            .IsEqualTo(Command.SetAirspacePath);

        await Assert.That(Keymap.EscapeHatch(inside)).IsEqualTo(KeyStroke.Esc)
            .Because("a mode owns the keyboard, and exactly one way out is what stops the "
                   + "terminal being locked up.");

        // EVERY LETTER FALLS THROUGH TO THE FIELD, which is the whole point of
        // making this a mode: `p` here is a character in a path, not a pull.
        foreach (var letter in "psmjkw")
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Char(letter), inside)).IsNull()
                .Because($"`{letter}` has to reach the field, or a path cannot contain it.");
        }
    }

    [Test]
    public async Task Applying_it_is_the_shell_s_work()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.SetAirspacePath)
            .Because("it writes a file, and this console's rule is that a write happens "
                   + "between sessions with the terminal provably free.");

        var before = new AppState { Mode = UiMode.AirspacePath };
        await Assert.That(Reducer.Reduce(before, Command.SetAirspacePath)).IsEqualTo(before);
    }

    [Test]
    public async Task Leaving_the_field_forgets_what_was_typed()
    {
        // OR A CANCELLED EDIT IS APPLIED BY THE NEXT ONE. The typed value
        // survives only as long as the question is open.
        var abandoned = Reducer.Reduce(
            new AppState { Mode = UiMode.AirspacePath, AirspacePathTyped = "/tmp/half-typed" },
            Command.CloseModal);

        await Assert.That(abandoned.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(abandoned.AirspacePathTyped).IsNull();
    }

    [Test]
    public async Task The_field_holds_the_keyboard_and_keeps_the_cursor()
    {
        // THE SCAR THIS FILE ALREADY CARRIES: moving focus on every render
        // turned a once-a-second countdown into a button that could be reached
        // and not held. A field is worse - re-asserting focus would move the
        // cursor while somebody is typing in it.
        await Assert.That(FocusChange.Wanted(
                UiMode.AirspacePath, TabId.Envelope, landed: null,
                modalHasFocus: false, pathHasFocus: false))
            .IsEqualTo(FocusTarget.AirspacePath);

        await Assert.That(FocusChange.Wanted(
                UiMode.AirspacePath, TabId.Envelope, landed: null,
                modalHasFocus: false, pathHasFocus: true))
            .IsEqualTo(FocusTarget.LeaveAlone)
            .Because("it already has the keyboard, and a render is not a reason to take it "
                   + "away and give it back at the start of the line.");
    }

    [Test]
    public async Task A_modal_still_wins_the_keyboard()
    {
        // THE CONTROL ON THE FOCUS CHANGE. Adding an arm above the generic
        // not-Normal one must not stop a real modal getting focus.
        await Assert.That(FocusChange.Wanted(
                UiMode.GateDecision, TabId.Queue, landed: null,
                modalHasFocus: false, pathHasFocus: false))
            .IsEqualTo(FocusTarget.Modal);
    }

    [Test]
    public async Task What_was_typed_is_what_lands_in_the_file()
    {
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-typed-{Guid.NewGuid():N}", "config.json");

        try
        {
            var said = ConsoleAirspacePath.Set(at, "/tmp/typed-in");

            await Assert.That(ConfigurationFile.Read(at).Configuration?.Airspace)
                .IsEqualTo("/tmp/typed-in")
                .Because("the file is the durable answer and the one the verbs and the doctor "
                       + "resolve through.");
            await Assert.That(said).Contains("/tmp/typed-in", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(at)!, recursive: true);
        }
    }

    [Test]
    public async Task An_empty_field_changes_nothing()
    {
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-typed-{Guid.NewGuid():N}", "config.json");

        var said = ConsoleAirspacePath.Set(at, "   ");

        await Assert.That(File.Exists(at)).IsFalse()
            .Because("somebody who opened the field and cleared it by accident must not end "
                   + "up with no airspace - an empty one makes every key on the tab refuse.");
        await Assert.That(said).Contains("unchanged", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task The_box_says_enter_begins_editing_it()
    {
        // TWO STAGES, AND THE TITLE IS WHERE THE SECOND ONE IS OFFERED. The box
        // holds the focus as soon as the tab does - so a person can see the
        // cursor in it, and copy the path out of it - and it takes no keystroke
        // until enter says so. A focusable field that started editing on
        // arrival would be the always-focused version this console measured
        // against: p, s and m would go into a path.
        foreach (var state in (AppState[])
        [
            new() { Estate = new EstateOnThisMachine { Root = "/tmp/somewhere" } },
            new() { Estate = new EstateOnThisMachine { Root = null } },
        ])
        {
            await Assert.That(PaneText.AirspaceBox(state))
                .Contains("enter", StringComparison.OrdinalIgnoreCase)
                .Because("focus alone does not say a field can be typed into, so the box "
                       + "says which key starts.");
        }
    }

    [Test]
    public async Task While_editing_the_box_says_how_to_stop()
    {
        var text = PaneText.AirspaceBox(new AppState
        {
            Mode = UiMode.AirspacePath,
            Estate = new EstateOnThisMachine { Root = "/tmp/somewhere" },
        });

        await Assert.That(text).Contains("esc", StringComparison.OrdinalIgnoreCase);
        await Assert.That(text).DoesNotContain(
                "enter to edit", StringComparison.OrdinalIgnoreCase)
            .Because("it is already being edited, and a title offering to start again is a "
                   + "title nobody can act on.");
    }

    [Test]
    public async Task The_field_routes_commands_rather_than_swallowing_them()
    {
        // THE HAZARD THIS WHOLE DESIGN IS SHAPED BY, and the only assertion
        // available for it without a terminal. A focused TextView consumes
        // printable keys - that is what ate all twenty-one of them through
        // TableView's type-to-search - so a field that holds focus while NOT
        // editing has to hand what it is given back to the one keymap rather
        // than keeping it. Asserted at the source because the alternative needs
        // a real terminal, and what would go wrong is silent: p would insert a
        // character instead of pulling.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gg.sln")))
        {
            root = root.Parent;
        }

        var source = File.ReadAllText(Path.Combine(
            root!.FullName, "Gg.Console", "Views", "ConsoleScreen.cs"));

        var handler = source[source.IndexOf(
            "private void OnAirspacePathKeyDown", StringComparison.Ordinal)..];
        handler = handler[..handler.IndexOf("\n    private", StringComparison.Ordinal)];

        await Assert.That(handler).Contains("Keymap.Resolve", StringComparison.Ordinal)
            .Because("while the field is not being edited its keys are the tab's, and the "
                   + "keymap is the one authority on what a key means. A handler that "
                   + "decided for itself would be a second keymap for one widget.");
    }

    [Test]
    public async Task The_current_directory_is_one_keystroke_while_editing()
    {
        // A CONTROL COMBINATION, BECAUSE EVERY PLAIN LETTER IS THE FIELD'S.
        // That is the deal this mode makes - `d` inside a path is a character -
        // so an affordance in here has to be a key a path cannot contain.
        await Assert.That(Keymap.Resolve(
                KeyStroke.Control('d'),
                new KeymapContext(UiMode.AirspacePath, TabId.Envelope)))
            .IsEqualTo(Command.AirspacePathFromCwd);
    }

    [Test]
    public async Task The_current_directory_is_not_offered_outside_the_field()
    {
        // OR IT IS A KEY THAT FILLS A FIELD NOBODY IS LOOKING AT. Every other
        // mode and the tab itself answer nothing for it.
        await Assert.That(Keymap.Resolve(
                KeyStroke.Control('d'), new KeymapContext(UiMode.Normal, TabId.Envelope)))
            .IsNull();
    }

    [Test]
    public async Task The_box_says_the_current_directory_is_available()
    {
        var text = PaneText.AirspaceBox(new AppState
        {
            Mode = UiMode.AirspacePath,
            Estate = new EstateOnThisMachine { Root = "/tmp/somewhere" },
        });

        await Assert.That(text).Contains("ctrl-d", StringComparison.OrdinalIgnoreCase)
            .Because("a control combination is not something anybody guesses, so the title "
                   + "that already names enter and esc names this one too.");
    }

    [Test]
    public async Task Where_gg_was_launched_from_is_a_local_fact()
    {
        // IN THE LOCAL FOLD, beside the machine name and the airspace path, for
        // their reason: it is process state this machine already has, and
        // nothing about it is the control plane's to answer. A screen that
        // read it directly would be a view holding a fact the model does not,
        // which is what makes a state dump unable to explain what was on the
        // screen.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gg.sln")))
        {
            root = root.Parent;
        }

        var source = File.ReadAllText(Path.Combine(root!.FullName, "Gg.Cli", "Program.cs"));
        var fold = source[source.IndexOf(
            "static AppState LocalFacts(", StringComparison.Ordinal)..];
        fold = fold[..fold.IndexOf("\n}", StringComparison.Ordinal)];

        await Assert.That(fold).Contains("Cwd", StringComparison.Ordinal)
            .Because("boot and refresh both run this fold, so the value is there before the "
                   + "key that uses it can be pressed.");
    }

    [Test]
    public async Task Every_mode_says_whether_it_draws_a_dialog()
    {
        // THE DECLARATION THREE RATCHETS NOW READ. They walked every non-Normal
        // mode demanding a title, a body arm and focus at the frame, because
        // every mode but this one is a dialog - so this one drew an empty dialog
        // over the field it exists to focus, and failed all three at once.
        // Making the assumption explicit is the fix; this is what stops the next
        // non-dialog mode discovering it the same way.
        var undeclared = Enum.GetValues<UiMode>()
            .Where(m => !Modals.IsDrawn(m) && !Modals.NotDrawn.ContainsKey(m))
            .ToList();

        await Assert.That(undeclared).IsEmpty()
            .Because("a mode that is neither drawn nor declared not to be falls through to "
                   + "whatever the screen does by default, which is how this one drew a "
                   + "frame over a text field. Found: " + string.Join(", ", undeclared));

        var both = Enum.GetValues<UiMode>()
            .Where(m => Modals.IsDrawn(m) && Modals.NotDrawn.ContainsKey(m))
            .ToList();

        await Assert.That(both).IsEmpty()
            .Because("exactly one of the two, or the reason written beside a mode describes "
                   + "something other than what the screen does. Found: "
                   + string.Join(", ", both));
    }

    [Test]
    public async Task The_field_s_mode_draws_no_dialog()
    {
        await Assert.That(Modals.IsDrawn(UiMode.AirspacePath)).IsFalse()
            .Because("a dialog would cover the field the mode exists to focus, which is "
                   + "exactly what it did before Modals existed.");
    }

    [Test]
    public async Task Nothing_says_press_w_any_more()
    {
        var text = PaneText.Estate(new AppState());

        await Assert.That(text).DoesNotContain("press w", StringComparison.OrdinalIgnoreCase)
            .Because("that flow is gone, and a pane advertising a key that no longer resolves "
                   + "is worse than one that never had it.");
    }
}
