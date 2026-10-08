namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.5-01</b> - an agent's exit removes only its row; the last agent's exit leaves gg as it
/// launches: the column with no agents in it (owner's call, 2026-10-07).
/// </summary>
public class ExitingAnAgentRemovesOnlyItsTabTests
{
    [Test]
    public async Task An_agent_that_ends_takes_only_its_own_row_and_leaves_what_it_said()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "a-end"));
        fixture.Agent("B", fixture.Speaks("b-first", "b", "b-later", "b-end"));

        fixture.Raise("a");
        fixture.Raise("a-end");
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 1)).IsTrue();
        await Assert.That(fixture.Mux.Rows() is [{ Number: 1, Label: "B" }]).IsTrue()
            .Because("B moves up to row one, and is the only row left.");

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Ending)).IsTrue();
        var folded = fixture.Mux.Fold(new AppState(), out var any);
        await Assert.That(any).IsTrue();
        await Assert.That(folded.LastNomination).IsEqualTo("A ended")
            .Because("what a session leaves is folded when its agent ends - a plan's proposal, a compose's flight.");
        await Assert.That(folded.Agents.Select(row => row.Label)).IsEquivalentTo(["B"]);
    }

    [Test]
    public async Task The_shown_agent_ending_shows_the_one_above_it_and_the_last_ending_is_gg()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "a-end"));
        fixture.Agent("B", fixture.Speaks("b-first", "b", "b-later", "b-end"));

        var showing = fixture.Showing(MuxTab.Agent(2));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("bar B", StringComparison.Ordinal)))
            .IsTrue();

        var from = fixture.Terminal.Painted.Length;
        fixture.Raise("b");
        fixture.Raise("b-end");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted[from..].Contains("bar A", StringComparison.Ordinal)))
            .IsTrue()
            .Because("B ended on screen, and the agent above it is where the eye already is.");

        fixture.Raise("a");
        fixture.Raise("a-end");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg)
            .Because("the last agent ending leaves gg.");

        var folded = fixture.Mux.Fold(new AppState(), out _);
        await Assert.That(folded.Agents).IsEmpty()
            .Because("no agent rows: gg is the mux view it launched as, with nobody in it.");
        await Assert.That(KeymapContext.For(folded).Agents).IsEqualTo(0);
    }
}
