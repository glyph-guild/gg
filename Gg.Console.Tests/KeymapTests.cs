namespace Gg.Console.Tests;

public class KeymapTests
{
    private static readonly KeyInfo Plain = new(Ctrl: false, Escape: false, Tab: false);

    [Test]
    public async Task QQuitsInNormalMode()
    {
        await Assert.That(Keymap.Resolve('q', Plain, new(UiMode.Normal))).IsEqualTo(Command.Quit);
    }

    [Test]
    public async Task CtrlCQuitsInAnyMode()
    {
        await Assert.That(Keymap.Resolve('c', Plain with { Ctrl = true }, new(UiMode.Help)))
            .IsEqualTo(Command.Quit);
    }

    [Test]
    public async Task QuestionMarkTogglesHelp()
    {
        await Assert.That(Keymap.Resolve('?', Plain, new(UiMode.Normal))).IsEqualTo(Command.ToggleHelp);
    }

    [Test]
    public async Task EscapeClosesHelpInsteadOfQuitting()
    {
        await Assert.That(Keymap.Resolve(null, Plain with { Escape = true }, new(UiMode.Help)))
            .IsEqualTo(Command.ToggleHelp);
    }

    [Test]
    public async Task QClosesHelpWhileHelpIsOpen()
    {
        await Assert.That(Keymap.Resolve('q', Plain, new(UiMode.Help))).IsEqualTo(Command.ToggleHelp);
    }

    [Test]
    public async Task TabFocusesNextPane()
    {
        await Assert.That(Keymap.Resolve(null, Plain with { Tab = true }, new(UiMode.Normal)))
            .IsEqualTo(Command.FocusNextPane);
    }

    [Test]
    public async Task EOpensTheEditor()
    {
        await Assert.That(Keymap.Resolve('e', Plain, new(UiMode.Normal))).IsEqualTo(Command.OpenEditor);
    }

    [Test]
    public async Task HelpModeSwallowsOtherBindings()
    {
        await Assert.That(Keymap.Resolve('e', Plain, new(UiMode.Help))).IsNull();
    }

    [Test]
    public async Task UnboundKeysResolveToNull()
    {
        await Assert.That(Keymap.Resolve('x', Plain, new(UiMode.Normal))).IsNull();
    }
}
