namespace Gg.Console.Tests;

/// <summary>
/// Found by the owner on the first local build of slice sixty-nine: the bar's panel no longer
/// scrolled.
/// </summary>
/// <remarks>
/// <b>The column turns mouse reporting on, and that changed what the wheel sends.</b> With
/// reporting off, a terminal on the alternate screen turns the wheel into arrow keys - which is how
/// the panel, and an agent that never asked for the mouse, scrolled. With it on, the wheel arrives
/// as reports, and the mux dropped them for a child that had not asked.
/// </remarks>
public class TheWheelStillScrollsBesideTheColumnTests
{
    [Test]
    public async Task A_wheel_over_an_agent_that_did_not_ask_for_the_mouse_arrives_as_arrows()
    {
        using var fixture = new MuxFixture();
        var took = new List<string>();

        fixture.Mux.Launch("A", () =>
        {
            fixture.Mux.Host(
                fixture.Terminal, "/bin/sh",
                ["-c", $"printf ready; while [ ! -f '{fixture.Flag("end")}' ]; do sleep 0.02; done"],
                fixture.Directory,
                (_, _) => new HostedRows(["bar A"], false),
                (gesture, typed) =>
                {
                    lock (took)
                    {
                        took.Add($"{gesture}:{System.Text.Encoding.ASCII.GetString(typed.Span)}");
                    }

                    return true;
                },
                CancellationToken.None).GetAwaiter().GetResult();
            return state => state;
        });

        var showing = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("ready", StringComparison.Ordinal)))
            .IsTrue();

        // THE WHEEL, DOWN THEN UP, over the agent's half of the screen.
        fixture.Terminal.Type("\u001b[<65;45;6M");
        fixture.Terminal.Type("\u001b[<64;45;6M");

        await Assert.That(MuxFixture.Until(() =>
            {
                lock (took)
                {
                    return took.Contains("Typed:\u001b[B") && took.Contains("Typed:\u001b[A");
                }
            }))
            .IsTrue()
            .Because("an agent that never asked for the mouse is sent what the terminal would have "
                   + "sent without the column - arrows - and the panel scrolls on them first.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }
}
