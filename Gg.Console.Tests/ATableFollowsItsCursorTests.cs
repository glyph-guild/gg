using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A cursor past the bottom edge has to take the view with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED IN A PTY, AND THE NUMBERS ARE THE WHOLE ARGUMENT.</b> The
/// flights tab held ninety-two rows. Forty arrow-downs moved the model's
/// cursor to row forty and the screen still showed rows one to thirty-four.
/// Another sixty moved the cursor to ninety-one and the screen showed the same
/// thirty-four. Reported as "the flights screen does not let me scroll very
/// far", which is exactly what it does.
/// </para>
/// <para>
/// <b>The cause is that the source is replaced on every render.</b>
/// <c>ConsoleScreen.Fill</c> builds a new <c>DataTableSource</c> each pass -
/// which is what makes the table a projection of the model rather than a second
/// copy of it - and a table handed a new source starts at the top. The
/// selection is then set from the model, and setting a selection moves no
/// scroll offset: the cursor sits on row ninety-one of a view showing row one.
/// </para>
/// <para>
/// <b>So the offset has to be asked for by name.</b>
/// <c>EnsureValidSelection</c>, which was being called, clamps the SELECTION to
/// the table's bounds - it says so in as many words. The one that moves the
/// view is <c>EnsureCursorIsVisible</c>: "updates scroll offsets to ensure that
/// the cursor cell is visible".
/// </para>
/// <para>
/// <b>Read off the source, because nothing here can render a table.</b>
/// <c>ConsoleScreen</c> cannot be constructed without a terminal and
/// Terminal.Gui 2.5.0 ships no fake driver, which is stated in the commit that
/// put scrollbars on these tables. The proof is the pty; this is the ratchet
/// that keeps the call.
/// </para>
/// </remarks>
public class ATableFollowsItsCursorTests
{
    [Test]
    public async Task Every_fill_moves_the_view_to_the_row_it_selected()
    {
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("EnsureCursorIsVisible")
            .Because("without it the table keeps the offset a new source gave it, which is "
                   + "zero - so every row past the first screenful is unreachable while the "
                   + "cursor walks happily past them.");

        var fill = screen[screen.IndexOf("private static void Fill<T>", StringComparison.Ordinal)..];
        fill = fill[..fill.IndexOf("\n    }", StringComparison.Ordinal)];

        await Assert.That(fill).Contains("EnsureCursorIsVisible")
            .Because("in Fill, which is the one place a table is given rows and a cursor - "
                   + "ten tables come through it and a second place to do this is a table "
                   + "somebody forgets.");

        await Assert.That(
            fill.IndexOf("SetSelection", StringComparison.Ordinal)
            < fill.IndexOf("EnsureCursorIsVisible", StringComparison.Ordinal))
            .IsTrue()
            .Because("the view is moved to where the cursor IS, so the cursor has to be put "
                   + "there first.");
    }
}
