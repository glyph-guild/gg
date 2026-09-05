namespace Gg.Console.Tests;

/// <summary>
/// The refresh key obeys the two rules every key here obeys.
/// </summary>
/// <remarks>
/// <b>Resolve is pure and the hint comes from Hints.</b> The non-negotiable, and
/// the reason is that a screen restating a binding is a second list: it drifts,
/// and the half nobody watches is the wrong one. Both halves read the same map.
/// </remarks>
public class RefreshKeyIsPureTests
{
    [Test]
    public async Task The_key_resolves_to_the_command_and_nothing_else_happens()
    {
        var context = new KeymapContext(UiMode.Normal);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('g'), context))
            .IsEqualTo(Command.Refresh);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('g'), context))
            .IsEqualTo(Command.Refresh)
            .Because("pure: asking twice answers twice the same, because resolving a key "
                   + "decides nothing and changes nothing.");
    }

    [Test]
    public async Task The_hint_comes_from_the_same_map_the_key_does()
    {
        var hints = Keymap.Hints(new KeymapContext(UiMode.Normal));

        await Assert.That(hints.Contains("refresh", StringComparison.Ordinal)).IsTrue()
            .Because("a screen that restated its own bindings would be a second list, and "
                   + "the one nobody watches is the one that goes wrong.");
    }
}
