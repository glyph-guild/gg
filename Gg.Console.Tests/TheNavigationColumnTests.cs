using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.2-01</b> - the column is thirty wide, in the logo's blue and never white, listing gg first,
/// each live agent, "+ new agent", and history at the bottom.
/// </summary>
public class TheNavigationColumnTests
{
    private static readonly IReadOnlyList<MuxRow> Two =
    [
        new(1, "plan · console", TimeSpan.FromMinutes(3), Changed: false),
        new(2, "Claude Code", TimeSpan.FromSeconds(40), Changed: false),
    ];

    [Test]
    public async Task It_lists_gg_the_agents_new_and_history_in_that_order()
    {
        var lines = MuxColumn.Lines(Two, MuxTab.Gg, height: 16);

        await Assert.That(lines.Count).IsEqualTo(16);
        await Assert.That(lines.All(line => line.Text.Length == MuxColumn.Width)).IsTrue()
            .Because("thirty columns on every row, or the agent beside it starts in a different place on each.");
        await Assert.That(lines[0].Text.Length).IsEqualTo(30);

        await Assert.That(lines[0].Text.TrimEnd()).IsEqualTo(" gg");
        await Assert.That(lines[0].Tab).IsEqualTo(MuxTab.Gg);
        await Assert.That(lines[2].Text).Contains("1 plan · console");
        await Assert.That(lines[2].Text).Contains("3m");
        await Assert.That(lines[3].Text).Contains("2 Claude Code");
        await Assert.That(lines[4].Text.TrimEnd()).IsEqualTo(" + new agent");
        await Assert.That(lines[^1].Text.TrimEnd()).IsEqualTo(" history")
            .Because("history is pinned to the bottom, however many agents there are.");
        await Assert.That(lines[^1].Tab).IsEqualTo(MuxTab.History);
    }

    [Test]
    public async Task The_shown_tab_is_the_active_row()
    {
        var lines = MuxColumn.Lines(Two, MuxTab.Agent(2), height: 16);

        await Assert.That(lines.Where(line => line.Active).Select(line => line.Tab))
            .IsEquivalentTo((MuxTab?[])[MuxTab.Agent(2)]);
    }

    [Test]
    public async Task It_is_painted_in_the_logo_s_blue_and_never_white()
    {
        var painted = MuxColumn.Paint(MuxColumn.Lines(Two, MuxTab.Gg, height: 16));

        await Assert.That(painted).Contains("48;2;124;176;204")
            .Because("the owner: 'don't make it white - make it same light blue as used as gg logo'.");
        await Assert.That(painted).DoesNotContain("[7m")
            .Because("inverse video is what the hosted bar uses, and most terminals show it white.");
        await Assert.That(painted).DoesNotContain("255;255;255");
        await Assert.That(painted).DoesNotContain("47m");

        var stamp = ConsoleTheme.Stamp().Normal.Foreground;
        await Assert.That($"{stamp.R};{stamp.G};{stamp.B}").IsEqualTo(MuxColumn.Blue)
            .Because("the logo's blue is the version badge's, and a second blue would be a second meaning.");
    }

    [Test]
    public async Task Beside_an_agent_the_column_is_painted_left_of_it()
    {
        using var fixture = new MuxFixture(columns: 70, rows: 12);
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "end"));

        var showing = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("a-first", StringComparison.Ordinal)))
            .IsTrue();

        var painted = fixture.Terminal.Painted;
        await Assert.That(painted).Contains("\u001b[1;1H\u001b[0;38;2;16;24;32;48;2;124;176;204m gg")
            .Because("gg is always the column's first row, in the blue, at the left edge.");
        await Assert.That(painted).Contains("\u001b[1;31H")
            .Because("the agent's bar starts in the first column past the nav.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }
}
