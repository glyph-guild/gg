using XTerm.Options;
using XTermTerminal = XTerm.Terminal;

namespace Gg.Console.Tests;

/// <summary>
/// gg's bar over a hosted child is dressed like the mux's column beside it - its darker blue,
/// with light ink - not in inverse video (owner, 2026-10-09: "change the white top gg bar to the
/// same color as the left bar? (and use white text)").
/// </summary>
public class TheBarIsTheColumnsColourTests
{
    private const string Esc = "\u001b";

    private static XTermTerminal Screen() => new(new TerminalOptions { Cols = 20, Rows = 4 });

    [Test]
    public async Task The_top_bar_is_the_column_s_ground_with_light_ink()
    {
        var frame = PtyScreen.Paint(Screen(), rows: 4, columns: 20, panel: ["gg · agent"]);

        await Assert.That(frame).Contains($"{Esc}[0;38;2;{MuxColumn.Light};48;2;{MuxColumn.Shade}mgg · agent");
        await Assert.That(frame).DoesNotContain($"{Esc}[7m")
            .Because("inverse video is what most terminals show white.");
    }

    [Test]
    public async Task The_footer_is_dressed_the_same()
    {
        var frame = PtyScreen.Paint(Screen(), rows: 3, columns: 20, panel: ["gg"], footer: "keys");

        await Assert.That(frame).Contains($"{Esc}[0;38;2;{MuxColumn.Light};48;2;{MuxColumn.Shade}mkeys");
        await Assert.That(frame).DoesNotContain($"{Esc}[7m");
    }
}
