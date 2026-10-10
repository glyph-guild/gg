using Gg.Console.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// A modal's shadow looks the same however the screen beneath it repaints.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported 2026-10-09:</b> with a modal up, the shadow flashed between the
/// text under it and black. The queue and panes beneath repaint on every
/// refresh, and the library's default transparent shadow is only redrawn when
/// the dialog is.
/// </para>
/// <para>
/// <b>The library's own driver, not a <c>ConsoleScreen</c>,</b> which nothing
/// can construct without a terminal. A dialog over a full screen of text is the
/// whole of the mechanism: which pass repaints which cell.
/// </para>
/// </remarks>
public class AModalShadowHoldsStillTests
{
    [Test]
    public async Task The_console_s_shadow_is_the_same_after_the_screen_beneath_repaints()
    {
        var seen = Frames(ConsoleTheme.ModalShadow);

        await Assert.That(seen.Distinct().Count()).IsEqualTo(1)
            .Because("a refresh repaints the queue under an open modal many times a minute, "
                   + "and each one that reaches the shadow is a flash. Seen: "
                   + string.Join(" | ", seen));
    }

    [Test]
    public async Task The_librarys_transparent_shadow_is_what_flashed()
    {
        // THE CONTROL. If an upgrade of Terminal.Gui makes this hold still, the
        // reason for ModalShadow has gone and the see-through look is back on
        // the table - this failing is the news, not a regression.
        var seen = Frames(ShadowStyles.Transparent);

        await Assert.That(seen.Distinct().Count()).IsGreaterThan(1)
            .Because("the transparent shadow dims the cell under it only in a pass that draws "
                   + "the dialog, so a repaint beneath alone shows that cell undimmed. Seen: "
                   + string.Join(" | ", seen));
    }

    /// <summary>
    /// One shadow cell, after: the first draw, the dialog redrawn, the screen
    /// beneath redrawn alone, and the dialog redrawn again.
    /// </summary>
    private static List<string> Frames(ShadowStyles style)
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);

        try
        {
            app.Driver!.SetScreenSize(30, 10);

            var top = new Runnable { Width = Dim.Fill(), Height = Dim.Fill() };
            var beneath = new Label
            {
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Text = string.Join('\n', Enumerable.Repeat(new string('X', 30), 10)),
            };
            var modal = new Dialog { X = 5, Y = 2, Width = 12, Height = 5, ShadowStyle = style };
            top.Add(beneath, modal);
            app.Begin(top);

            // THE RIGHT-HAND SHADOW, halfway down: the last column of the
            // dialog's frame, which the margin's shadow occupies.
            var frame = modal.Frame;
            var row = frame.Y + 2;
            var column = frame.X + frame.Width - 1;

            string Cell()
            {
                var cell = app.Driver.Contents![row, column];
                return $"'{cell.Grapheme}' {cell.Attribute}";
            }

            List<string> seen = [Cell()];

            modal.SetNeedsDraw();
            app.LayoutAndDraw();
            seen.Add(Cell());

            beneath.SetNeedsDraw();
            app.LayoutAndDraw();
            seen.Add(Cell());

            modal.SetNeedsDraw();
            app.LayoutAndDraw();
            seen.Add(Cell());

            top.Dispose();

            return seen;
        }
        finally
        {
            app.Dispose();
        }
    }
}
