using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A page for trying looks on, and a way to hand back the ones that worked.
/// </summary>
/// <remarks>
/// <para>
/// <b>A SPIKE, and it says so where somebody would find out too late.</b> What
/// is being tried is whether the look of this console is worth making
/// adjustable; the copy is how the answer leaves the spike. A person changes
/// two things out of seven, presses <c>c</c>, and is handed the two — not a
/// dump of a model, and not a listing of five defaults with their two buried
/// in it.
/// </para>
/// <para>
/// <b>The colours are ours rather than Terminal.Gui's.</b> The library has a
/// <c>ThemeManager</c> with named themes, and reaching it means enabling
/// <c>ConfigurationManager</c> — reflection-driven JSON, which this binary may
/// not have and which CI would catch at the AOT step rather than here. So the
/// palettes are data in the model and schemes in the view layer, which is the
/// split <c>KeyTranslator</c> already is for keys.
/// </para>
/// <para>
/// <b>Everything cycles and wraps, except the two that are numbers.</b> A
/// modal has few letters to spare, and a value that wraps needs one key rather
/// than two. Numbers do not wrap, because a depth that jumps from 5 to 1 reads
/// as a bug rather than as a cycle.
/// </para>
/// </remarks>
public class TheLookPageTests
{
    [Test]
    public async Task It_is_a_page_of_the_help_modal()
    {
        // A TAB, WHICH MEANS THE BAR AND THE WALK BOTH KNOW IT. HelpPages.All
        // is what the strip draws and what `tab' cycles, so a page added to the
        // enum and not to the list is one nothing can reach.
        await Assert.That(HelpPages.All).Contains(HelpPage.Look);

        await Assert.That(HelpPages.Title(HelpPage.Look)).IsEqualTo("look")
            .Because("a page with no title draws its enum name, which is the same word "
                   + "by luck rather than by decision.");
    }

    [Test]
    public async Task Every_setting_says_what_it_is_for()
    {
        // A VALUE WITH NO SENTENCE BESIDE IT IS A KNOB. "RoundedDashed" tells
        // somebody what glyph is drawn and nothing about where.
        foreach (var setting in Looks.All)
        {
            await Assert.That(Looks.Title(setting)).IsNotEmpty();

            await Assert.That(Looks.About(setting)).IsNotEmpty()
                .Because($"{setting} is offered on a page somebody is reading to find out "
                       + "what it does.");
        }
    }

    [Test]
    public async Task Turning_a_value_moves_the_setting_under_the_cursor()
    {
        var look = new Look { Selected = 0 };

        var turned = Looks.Turned(look, +1);

        await Assert.That(turned.Palette).IsNotEqualTo(look.Palette);
        await Assert.That(turned.PaneBorder).IsEqualTo(look.PaneBorder)
            .Because("one key changes one thing, and the cursor is what says which.");
    }

    [Test]
    public async Task An_enum_wraps_at_both_ends()
    {
        // ONE KEY IS ENOUGH BECAUSE IT WRAPS. Eleven line styles is a long way
        // back if walking off the end strands you.
        var last = Looks.All.ToList().IndexOf(LookSetting.PaneBorder);
        var look = new Look { Selected = last, PaneBorder = Edge.None };

        var back = Looks.Turned(look, -1);

        await Assert.That(back.PaneBorder).IsEqualTo(Edge.RoundedDotted)
            .Because("stepping back off the front lands on the end - C# gives -1 % 11 as "
                   + "-1, which indexes nothing, so this is the arithmetic being asserted.");

        await Assert.That(Looks.Turned(back, +1).PaneBorder).IsEqualTo(Edge.None)
            .Because("and forward off the end comes round.");
    }

    [Test]
    public async Task A_number_is_bounded_rather_than_wrapped()
    {
        var at = Looks.All.ToList().IndexOf(LookSetting.TabDepth);
        var look = new Look { Selected = at, TabDepth = 1 };

        await Assert.That(Looks.Turned(look, -1).TabDepth).IsEqualTo(1)
            .Because("a depth that jumped from its smallest to its largest would read as a "
                   + "bug rather than as a cycle - and one row is the shallowest tab that "
                   + "can hold a title.");

        await Assert.That(Looks.Turned(look with { TabDepth = 5 }, +1).TabDepth).IsEqualTo(5);
    }

    [Test]
    public async Task The_cursor_is_clamped_rather_than_trusted()
    {
        // THE CURSOR OUTLIVES NOTHING HERE, and it is still a number somebody
        // could set past the end - a serialized state, a click on a table that
        // has since been refilled. Rendering nothing reads as a broken page.
        await Assert.That(Looks.Under(new Look { Selected = 99 }))
            .IsEqualTo(Looks.All[^1]);

        await Assert.That(Looks.Under(new Look { Selected = -3 }))
            .IsEqualTo(Looks.All[0]);
    }

    [Test]
    public async Task A_console_that_has_been_left_alone_has_nothing_to_hand_back()
    {
        // AND SAYS SO, rather than copying a header over nothing.
        // ConsoleClipboard refuses whitespace and reports that it did; a
        // heading with no body gets past that check and pastes as a heading.
        var said = Looks.Copyable(new Look());

        await Assert.That(Looks.Changed(new Look())).IsEmpty();
        await Assert.That(said).Contains("as it ships");
        await Assert.That(said.Trim()).IsNotEmpty();
    }

    [Test]
    public async Task What_is_copied_is_the_changes_and_not_the_model()
    {
        // THE WHOLE POINT OF THE SPIKE LEAVING THE SPIKE. Two things were
        // changed out of seven; those two are what somebody wants handed back.
        var look = new Look { Palette = Palette.Midnight, ModalBorder = Edge.Double };

        var said = Looks.Copyable(look);

        await Assert.That(said).Contains("Midnight");
        await Assert.That(said).Contains("Double");

        await Assert.That(said).DoesNotContain("tab depth")
            .Because("five settings were left alone, and a copy that listed them buries "
                   + "the two that are the answer.");

        await Assert.That(said).Contains("ships as")
            .Because("what it was before is what makes the instruction reviewable - "
                   + "somebody reading it should not have to open gg to know what moved.");
    }

    [Test]
    public async Task The_rows_say_which_settings_were_moved()
    {
        var look = new Look { Palette = Palette.Neon };

        var rows = Looks.Rows(look);

        await Assert.That(rows).Count().IsEqualTo(Looks.All.Count);
        await Assert.That(rows[0].Value).IsEqualTo("Neon");
        await Assert.That(rows[0].Changed).IsNotEmpty();

        await Assert.That(rows[1].Changed).IsEmpty()
            .Because("a page where everything claims to have changed says nothing about "
                   + "what has.");
    }

    [Test]
    public async Task The_look_survives_a_terminal_release()
    {
        // THE PROPERTY THAT MAKES ALL OF THIS SAFE. AppState is what a rebuilt
        // session is drawn from, so a look that did not serialize would be a
        // console that reverted every time somebody opened an editor.
        var state = new AppState { Look = new Look { Palette = Palette.Amber, TabDepth = 4 } };

        var json = System.Text.Json.JsonSerializer.Serialize(
            state, AppStateJsonContext.Default.AppState);

        var back = System.Text.Json.JsonSerializer.Deserialize(
            json, AppStateJsonContext.Default.AppState);

        await Assert.That(back!.Look.Palette).IsEqualTo(Palette.Amber);
        await Assert.That(back.Look.TabDepth).IsEqualTo(4);
    }

    [Test]
    public async Task The_state_holds_no_library_type()
    {
        // THE RULE THAT MAKES TERMINAL RELEASE POSSIBLE, asserted on the one
        // record most likely to break it: every value here has a Terminal.Gui
        // spelling, and the obvious way to write this holds LineStyle directly.
        var offenders = typeof(Look).GetProperties()
            .Where(p => p.PropertyType.Namespace?.StartsWith(
                "Terminal.Gui", StringComparison.Ordinal) == true)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("AppState stays plain serializable data, and one place - "
                   + "Views/LookStyles - knows what a widget calls each of these. Found: "
                   + string.Join(", ", offenders));
    }
}
