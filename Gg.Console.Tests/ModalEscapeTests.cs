namespace Gg.Console.Tests;

/// <summary>
/// No modal can trap the terminal.
/// </summary>
/// <remarks>
/// <para>
/// The claim is quantified over ANY state and ANY sequence of keys, and a
/// claim of that shape cannot be established by a list of examples. So this
/// drives arbitrary keystrokes from arbitrary reachable states and asserts the
/// escape hatch always gets back out.
/// </para>
/// <para>
/// What a failure here looks like in the world: somebody's terminal stops
/// responding, they kill it, and whatever the console was holding is
/// gone - and if the console had been attached to a flight, the account of
/// what happened goes with it.
/// </para>
/// </remarks>
public class ModalEscapeTests
{
    private const int Sequences = 500;
    private const int MaxLength = 25;

    /// <summary>Applies a key the way the screen does: resolve, then reduce.</summary>
    private static AppState Press(AppState state, KeyStroke key)
    {
        var context = new KeymapContext(state.Mode, state.LiveVisible, state.Frozen);
        var command = Keymap.Resolve(key, context);

        // THE SAME DECLARATION THE SCREEN READS. This held a third literal copy of
        // the list, which is worth naming: a property proven over 500 generated key
        // sequences is worth exactly as much as the dispatch it models, and a copy
        // that drifted would keep proving something about a console that no longer
        // exists.
        return command is null || ShellCommands.Handled.Contains(command.Value)
            ? state
            : Reducer.Reduce(state, command.Value);
    }

    [Test]
    public async Task The_escape_hatch_always_returns_to_a_non_modal_state()
    {
        for (var seed = 0; seed < Sequences; seed++)
        {
            var random = new Random(seed);
            var state = StateGenerator.Next(random);

            for (var step = 0; step < random.Next(1, MaxLength); step++)
            {
                state = Press(state, KeymapTests.Universe[random.Next(KeymapTests.Universe.Count)]);
            }

            if (state.Mode == UiMode.Normal)
            {
                continue;
            }

            var hatch = Keymap.EscapeHatch(new KeymapContext(state.Mode, state.LiveVisible, state.Frozen));
            await Assert.That(hatch).IsNotNull().Because($"seed {seed} reached {state.Mode} with no way out.");

            var after = Press(state, hatch!.Value);

            await Assert.That(after.Mode).IsEqualTo(UiMode.Normal)
                .Because($"seed {seed}: {state.Mode} did not release the keyboard.");
        }
    }

    [Test]
    public async Task Some_of_those_sequences_actually_reached_a_modal()
    {
        // Guards the property above. If no sequence ever opened a modal, every
        // iteration would hit the `continue` and the test would pass without
        // testing anything - which is the same shape as a poison twin nobody
        // planted.
        var reached = 0;

        for (var seed = 0; seed < Sequences; seed++)
        {
            var random = new Random(seed);
            var state = StateGenerator.Next(random);

            for (var step = 0; step < random.Next(1, MaxLength); step++)
            {
                state = Press(state, KeymapTests.Universe[random.Next(KeymapTests.Universe.Count)]);
            }

            if (state.Mode != UiMode.Normal)
            {
                reached++;
            }
        }

        await Assert.That(reached).IsGreaterThan(Sequences / 20)
            .Because($"only {reached} of {Sequences} sequences ended in a modal; the property is nearly vacuous.");
    }

    [Test]
    public async Task Every_modal_is_reachable_from_a_fresh_console()
    {
        // A modal nobody can open cannot trap anybody, so the property above
        // would hold trivially for a mode that is simply unreachable. This is
        // the other half: each one can genuinely be entered.
        foreach (var mode in Enum.GetValues<UiMode>().Where(m => m != UiMode.Normal))
        {
            var opened = KeymapTests.Universe
                .Select(key => Press(new AppState(), key))
                .Any(state => state.Mode == mode);

            await Assert.That(opened).IsTrue().Because($"{mode} cannot be opened by any key.");
        }
    }

    [Test]
    public async Task Interrupt_ends_the_session_from_inside_any_modal()
    {
        // The last resort, independent of the escape hatch: even a modal that
        // somehow lost its way out cannot outlive ctrl+c.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            await Assert.That(Keymap.Resolve(Keymap.Interrupt, new KeymapContext(mode)))
                .IsEqualTo(Command.Quit);
        }
    }

    [Test]
    public async Task No_key_sequence_can_leave_focus_on_a_hidden_pane()
    {
        // Focus on a pane that is not on screen looks exactly like a frozen
        // keyboard, and the person's next move is to kill the terminal - the
        // same outcome the escape hatch exists to prevent, arrived at from a
        // different direction.
        for (var seed = 0; seed < Sequences; seed++)
        {
            var random = new Random(seed);
            var state = StateGenerator.Next(random) with { FocusedPane = PaneId.Queue };

            for (var step = 0; step < random.Next(1, MaxLength); step++)
            {
                state = Press(state, KeymapTests.Universe[random.Next(KeymapTests.Universe.Count)]);

                var visible = state.FocusedPane switch
                {
                    PaneId.Evidence => state.EvidenceVisible,
                    PaneId.Live => state.LiveVisible,
                    _ => true,
                };

                await Assert.That(visible).IsTrue()
                    .Because($"seed {seed} left focus on {state.FocusedPane}, which is not on screen.");
            }
        }
    }
}
