namespace Gg.Console.Tests;

public class HintsTests
{
    [Test]
    public async Task NormalModeAdvertisesEveryLiveBinding()
    {
        var hints = Keymap.Hints(new(UiMode.Normal));
        var plain = new KeyInfo(Ctrl: false, Escape: false, Tab: false);

        // Every advertised key must actually resolve in this context.
        await Assert.That(hints).IsEqualTo("q quit · ? help · e edit notes · tab switch pane");
        await Assert.That(Keymap.Resolve('q', plain, new(UiMode.Normal))).IsNotNull();
        await Assert.That(Keymap.Resolve('?', plain, new(UiMode.Normal))).IsNotNull();
        await Assert.That(Keymap.Resolve('e', plain, new(UiMode.Normal))).IsNotNull();
        await Assert.That(Keymap.Resolve(null, plain with { Tab = true }, new(UiMode.Normal))).IsNotNull();
    }

    [Test]
    public async Task HelpModeAdvertisesTheEscapeHatch()
    {
        var hints = Keymap.Hints(new(UiMode.Help));
        var plain = new KeyInfo(Ctrl: false, Escape: false, Tab: false);

        await Assert.That(hints).IsEqualTo("esc close help");
        await Assert.That(Keymap.Resolve(null, plain with { Escape = true }, new(UiMode.Help))).IsNotNull();
    }
}
