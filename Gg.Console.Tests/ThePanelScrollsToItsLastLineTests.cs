namespace Gg.Console.Tests;

/// <summary>
/// Found by the owner walking slice sixty-nine: scrolled to the bottom, the plan panel's last line
/// was not shown.
/// </summary>
/// <remarks>
/// <b>The clamp counted one row of status; the render counts what it wraps to.</b> Beside the
/// column a hosted bar is thirty columns narrower, and the plan session's long status wraps to two
/// or three rows - so the wheel stopped one or two lines short of the end.
/// </remarks>
public class ThePanelScrollsToItsLastLineTests
{
    [Test]
    public async Task Scrolled_as_far_as_it_goes_the_last_line_is_on_screen()
    {
        const string status = "gg · planning with an agent — ask it to draft legs · ctrl-g shows the plan, "
                            + "where J/K move a leg and x drops one · ask it to propose when it is ready";
        var body = string.Join('\n', Enumerable.Range(1, 40).Select(n => $"line {n}"));
        const int most = 12;
        const int columns = 60;

        var panel = new HostedPanel(HostedView.Plan, 0);
        for (var turn = 0; turn < 40; turn++)
        {
            panel = HostedBar.Next(panel, HostedGesture.ScrolledDown, [], body, most, columns,
                opensOn: HostedView.Plan, status: status);
        }

        var shown = HostedBar.Rows(panel, status, body, most, columns);

        await Assert.That(shown.Any(row => row.Trim() == "line 40")).IsTrue()
            .Because("the wheel stops where the last line comes to rest at the bottom, counting the "
                   + "rows the status really takes. Shown:\n" + string.Join('\n', shown));
    }
}
