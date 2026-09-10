using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// `o` on the Environment page takes the offer the page just named.
/// </summary>
/// <remarks>
/// <para>
/// <b>A write, so it is the loop's.</b> Taking an offer writes this machine's
/// configuration file, and a session may not write — the rule
/// <c>EditConfiguration</c> is in <c>ShellCommands.Handled</c> for, one key
/// along. The session ends, the write happens with the terminal free, and the
/// views are rebuilt from the model.
/// </para>
/// <para>
/// <b>It takes the version the page showed, not whatever is offered now.</b>
/// Between a person reading the page and pressing the key, a control plane can
/// change its offer — and applying the replacement to somebody who never saw it
/// is exactly what <c>gg config accept &lt;version&gt;</c> refuses on the
/// command line. Same guard, reached by a key instead of by typing.
/// </para>
/// <para>
/// <b>One letter, and it is safe because the modal owns the keyboard.</b> The
/// argument <c>e</c> is bound on: only this modal's keys resolve while it is
/// open, so a letter that means something else in Normal mode cannot reach
/// through.
/// </para>
/// </remarks>
public class TheConsoleTakesTheOfferItShowedTests
{
    /// <summary>One session that presses the key, then one that quits.</summary>
    /// <remarks>
    /// Two, because the command is handled BETWEEN sessions: a loop given one
    /// session would return before the arm under test had run.
    /// </remarks>
    private sealed class Presses(Command command) : IUiSession
    {
        private readonly Queue<Command> _script = new([command, Command.Quit]);

        public UiOutcome Run(AppState state) => new(_script.Dequeue(), state);
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }

    [Test]
    public async Task The_key_resolves_in_the_help_modal()
    {
        var resolved = Keymap.Resolve(
            KeyStroke.Char('o'), new KeymapContext { Mode = UiMode.Help });

        await Assert.That(resolved).IsEqualTo(Command.TakeOfferedConfiguration)
            .Because("the Environment page tells a person to press it, and a key a page "
                   + "advertises and the keymap does not resolve is the dead-key shape this "
                   + "estate keeps finding.");
    }

    [Test]
    public async Task It_is_the_loops_rather_than_the_sessions()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.TakeOfferedConfiguration)
            .Because("it writes this machine's configuration file, and a session may not "
                   + "write - which is why EditConfiguration is in this set beside it.");
    }

    [Test]
    public async Task Nothing_happens_on_a_console_that_was_not_wired_to_take_one()
    {
        // THE DEAD-ARM SHAPE, and this console has shipped it before: a key
        // that resolves, reaches its arm, and changes nothing. Saying so is
        // what makes the difference visible between "not configured" and
        // "pressed and did nothing".
        var loop = new ConsoleLoop(new Presses(Command.TakeOfferedConfiguration), new NoEditor());

        var after = loop.Run(new AppState { Mode = UiMode.Help });

        await Assert.That(after.LastConfiguration).IsNotNull();
        await Assert.That(after.LastConfiguration!).Contains("not configured", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_wired_console_is_handed_the_version_that_was_on_the_screen()
    {
        string? asked = null;

        var loop = new ConsoleLoop(
            new Presses(Command.TakeOfferedConfiguration),
            new NoEditor(),
            takeOffered: (state, version) =>
            {
                asked = version;
                return state with { LastConfiguration = $"took {version}" };
            });

        var after = loop.Run(new AppState
        {
            Mode = UiMode.Help,
            Offered = new OfferedOnThisMachine { Version = "offer@7", Settings = 1 },
        });

        await Assert.That(asked).IsEqualTo("offer@7")
            .Because("a control plane can change its offer between somebody reading the "
                   + "page and pressing the key, and applying the replacement to a person "
                   + "who never saw it is what accepting by version refuses.");

        await Assert.That(after.LastConfiguration).IsEqualTo("took offer@7");
    }

    [Test]
    public async Task Pressing_it_with_nothing_offered_says_so_and_asks_nobody()
    {
        var asked = false;

        var loop = new ConsoleLoop(
            new Presses(Command.TakeOfferedConfiguration),
            new NoEditor(),
            takeOffered: (state, _) =>
            {
                asked = true;
                return state;
            });

        var after = loop.Run(new AppState { Mode = UiMode.Help, Offered = null });

        await Assert.That(asked).IsFalse()
            .Because("there is nothing to take, and a round trip to find that out again is "
                   + "one the page already made.");
        await Assert.That(after.LastConfiguration!).Contains("Nothing", StringComparison.Ordinal);
    }
}
