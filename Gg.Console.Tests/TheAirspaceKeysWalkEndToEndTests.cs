using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Every airspace act is reachable by pressing keys, from a console on the
/// Envelope tab, and each step advertises the next one.
/// </summary>
/// <remarks>
/// <para>
/// <b>PER-LINK TESTS CANNOT SEE A BROKEN CHAIN.</b> Each hop in these paths is
/// asserted somewhere — the keymap resolves, the reducer opens, the command is
/// the shell's, the port is passed — and that is exactly the shape this
/// repository has shipped broken more than once: five separate components,
/// each green, with nothing joining two of them. So this walks the whole way
/// with nothing but keystrokes.
/// </para>
/// <para>
/// <b>And it checks the hint line at every step</b>, because a key that works
/// and is not advertised is a key nobody presses. The three reading views are
/// reached by letters inside a modal, which means the only way anybody learns
/// them is the line at the bottom.
/// </para>
/// <para>
/// <b>What it deliberately does NOT do is call the ports.</b> Those make
/// requests; the claim here is that pressing keys arrives at them, which is
/// the half that kept going missing.
/// </para>
/// </remarks>
public class TheAirspaceKeysWalkEndToEndTests
{
    /// <summary>A console on the airspace tab, with a tree and a diff read.</summary>
    private static AppState Loaded() => new()
    {
        ActiveTab = TabId.Envelope,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",
            Uncommitted = [],
            Names = new Gg.Contracts.EnvelopeTopology
            {
                Names =
                [
                    new Gg.Contracts.TopologyName
                    {
                        Name = "root",
                        Role = Gg.Contracts.Roles.Root,
                        DeclaredBy = "the floor exists; nobody declares it",
                        DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
            Working = new EstateDiff
            {
                Changes = [],
                Retiring = ["old-pci"],
                Unreadable = [],
            },
            Tree = new WorkingCopy
            {
                Present = true,
                Documents = [new("root", "root", "airspace/root.yaml", "v6")],
                Unreadable = [],
            },
        },
    };

    /// <summary>Presses a key the way the screen does, and says what happened.</summary>
    private static (AppState State, Command? Shell) Press(AppState state, KeyStroke key)
    {
        var command = Keymap.Resolve(key, KeymapContext.For(state));

        if (command is null)
        {
            return (state, null);
        }

        // THE SCREEN'S OWN SPLIT. A command the shell handles ends the session
        // and never reaches the reducer; everything else is reduced in place.
        return ShellCommands.Handled.Contains(command.Value)
            ? (state, command.Value)
            : (Reducer.Reduce(state, command.Value), null);
    }

    private static string Hints(AppState state) => Keymap.Hints(KeymapContext.For(state));

    [Test]
    public async Task Reading_the_rules_the_diff_and_the_last_apply_is_three_keypresses()
    {
        var state = Loaded();

        await Assert.That(Hints(state)).Contains("o ", StringComparison.Ordinal)
            .Because("the way in has to be on the line, or nothing below it is reachable "
                   + "by anybody who does not already know. `v' turns the pane beside the "
                   + "tree now; `o' opens what is about the airspace as a whole. Line: "
                   + Hints(state));

        (state, _) = Press(state, KeyStroke.Char('o'));
        await Assert.That(state.Mode).IsEqualTo(UiMode.ReadingOutcome);

        (state, _) = Press(state, KeyStroke.Char('e'));
        await Assert.That(state.Mode).IsEqualTo(UiMode.ReadingEnvelope);

        await Assert.That(Hints(state)).Contains("d ", StringComparison.Ordinal)
            .Because("inside the modal the letters are free, so the line is the ONLY place "
                   + "somebody learns them. Line: " + Hints(state));

        (state, _) = Press(state, KeyStroke.Char('d'));
        await Assert.That(state.Mode).IsEqualTo(UiMode.ReadingChangeset);

        (state, _) = Press(state, KeyStroke.Char('d'));
        await Assert.That(state.Mode).IsEqualTo(UiMode.ReadingChangeset)
            .Because("all three reach each other, which is why they share one box.");

        (state, _) = Press(state, KeyStroke.Esc);
        await Assert.That(state.Mode).IsEqualTo(UiMode.Normal)
            .Because("one way out, from every view.");
    }

    [Test]
    public async Task Applying_walks_from_the_tab_to_a_command_the_loop_handles()
    {
        var state = Loaded();

        await Assert.That(Hints(state)).Contains("s ", StringComparison.Ordinal);

        (state, var shell) = Press(state, KeyStroke.Char('s'));

        await Assert.That(state.Mode).IsEqualTo(UiMode.ConfirmApply);
        await Assert.That(shell).IsNull()
            .Because("asking is a mode change and nothing else.");

        await Assert.That(PaneText.Modal(state)).IsNotEmpty()
            .Because("a question with no body is a title over nothing.");

        (_, shell) = Press(state, KeyStroke.Char('y'));

        await Assert.That(shell).IsEqualTo(Command.ApplyEstate)
            .Because("answering ends the session, because applying makes requests - and "
                   + "this is the step that was unreachable when the whole feature was "
                   + "green and inert.");
    }

    [Test]
    public async Task Retiring_walks_from_the_tab_through_the_changeset_to_the_loop()
    {
        var state = Loaded();

        (state, _) = Press(state, KeyStroke.Char('o'));
        (state, _) = Press(state, KeyStroke.Char('d'));

        await Assert.That(state.Mode).IsEqualTo(UiMode.ReadingChangeset);

        // THE ONLY ADVERTISEMENT IT HAS. Retiring is three keys deep and on no
        // tree row, so if the line does not carry it nobody finds it.
        await Assert.That(Hints(state)).Contains("x ", StringComparison.Ordinal)
            .Because("this is the only place `x' is offered. Line: " + Hints(state));

        await Assert.That(string.Join('\n', PaneText.ChangesetLines(state, 0)))
            .Contains("old-pci", StringComparison.Ordinal)
            .Because("and the name it would retire is in the view that offers the key.");

        (state, var shell) = Press(state, KeyStroke.Char('x'));

        await Assert.That(state.Mode).IsEqualTo(UiMode.ConfirmRetire);
        await Assert.That(shell).IsNull();

        await Assert.That(PaneText.Modal(state))
            .Contains("old-pci", StringComparison.Ordinal)
            .Because("the question names what it would retire, because reversing one needs "
                   + "an approver.");

        (_, shell) = Press(state, KeyStroke.Char('y'));

        await Assert.That(shell).IsEqualTo(Command.RetireNames)
            .Because("and answering reaches the loop, which is where the requests happen.");
    }

    [Test]
    public async Task Pulling_and_drafting_are_still_one_keypress_each()
    {
        // THE NEIGHBOURS, so a letter added to this tab cannot quietly take one
        // of them over. `x` was very nearly put here, where it would have
        // shadowed forget-credential without a word.
        var state = Loaded();

        await Assert.That(Press(state, KeyStroke.Char('p')).Shell)
            .IsEqualTo(Command.PullEstate);

        await Assert.That(Press(state, KeyStroke.Char('m')).Shell)
            .IsEqualTo(Command.DraftEstate);

        await Assert.That(Press(state, KeyStroke.Char('x')).Shell)
            .IsEqualTo(Command.ForgetCredential)
            .Because("still the global, on this tab, because retiring went into the modal "
                   + "instead of shadowing it.");
    }

    [Test]
    public async Task Every_airspace_act_the_keys_reach_is_wired_at_the_root()
    {
        // THE OTHER HALF, AND THE ONE THAT WENT MISSING BEFORE. A command the
        // keys reach and the loop has an arm for is still inert if the
        // composition root passed no port - so this reads the one
        // `new ConsoleLoop(...)` and requires each by name. EveryPortIsPassedTests
        // asserts the same property over every port; this names the four this
        // tab depends on, so a failure here says which key went dead.
        var root = Sources.Read("Gg.Cli", "Program.cs");

        var call = root[root.IndexOf("new ConsoleLoop(", StringComparison.Ordinal)..];

        foreach (var port in (string[])
                 ["applyEstate:", "pullEstate:", "draftEstate:", "retireNames:"])
        {
            await Assert.That(call.Contains(port, StringComparison.Ordinal)).IsTrue()
                .Because($"{port} is what makes its key do anything. A key that resolves, "
                       + "reaches its arm and finds no port answers `this console is not "
                       + "configured to...', which is the dead-key shape this console has "
                       + "hit more than once.");
        }
    }
}
