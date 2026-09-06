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
        var context = new KeymapContext(state.Mode, state.ActiveTab, state.Frozen);
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

            var hatch = Keymap.EscapeHatch(new KeymapContext(state.Mode, state.ActiveTab, state.Frozen));
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

    /// <summary>
    /// Modes no single keystroke can open, and what proves them instead.
    /// </summary>
    /// <remarks>
    /// <b>The walk below presses one key against a FRESH state</b>, which is
    /// the right shape for a modal a key opens and cannot see one the LOOP
    /// opens after a read. An entry here has to say what proves the same
    /// property by another route, or it is just a hole.
    /// </remarks>
    private static readonly Dictionary<UiMode, string> OpenedByTheLoop = new()
    {
        [UiMode.SignIn] =
            "opened by ConsoleStart.LoadAsync when the control plane refuses the load "
          + "because nobody is signed in - which is a read, so no key can reach it and no "
          + "key should: it is not a question somebody asks, it is the reason the console "
          + "behind it is empty. SigningInFromTheConsoleTests drives the real loader into "
          + "it and asserts an unreachable control plane does NOT open it. Being escapable "
          + "is covered above rather than separately: StateGenerator emits every UiMode, so "
          + "The_escape_hatch_always_returns_to_a_non_modal_state already walks arbitrary "
          + "key sequences out of this one.",

        [UiMode.ConfirmFlight] =
            "opened by ConsoleLoop.FlewPicked after asking the control plane whether this "
          + "work item has already flown - a read, so it cannot happen inside a UI session "
          + "and cannot be reached by pressing a key against a fresh state. The property "
          + "this test guards, that a modal nobody can open cannot trap anybody, is proved "
          + "for it by ASecondFlightIsWarnedAboutTests: it is entered through FlyPicked and "
          + "left through CloseModal, and Every_modal_has_exactly_one_escape_hatch above "
          + "already covers it.",

        [UiMode.FlightDetail] =
            "opened by ConsoleLoop's ShowFlight arm after reading that flight's log - a "
          + "read, so it cannot happen inside a UI session. The boot fetches a log only "
          + "for a flight still in the air, and the modal is usually opened on one that "
          + "landed, so opening it from a key would show `no log fetched' over a pane "
          + "that never comes back to correct itself. Entered through Reducer.FlightShown "
          + "and left through CloseModal; TheBootReadsWhatItShowsTests drives the real "
          + "loop into it and asserts the log it renders is the one just read.",
    };

    /// <summary>The smallest list a cursor can point into.</summary>
    private static Gg.Contracts.FlightList OneFlight() => new()
    {
        Flights =
        [
            new Gg.Contracts.FlightSummary
            {
                FlightId = "01a0776a-cacb-76dc-b444-2b7031e840d8",
                FlightNumber = "GG-52",
                Name = "create a PR for a python script",
                Intent = new Gg.Contracts.FlightIntent
                {
                    Kind = Gg.Contracts.FlightIntentKinds.Text,
                    Text = "create a PR for a python script",
                },
                CreatedAt = DateTimeOffset.UnixEpoch,
                RunnerProtocolVersion = 1,
                FactVocabularyVersion = "0.25.0",
                ConstitutionVersion = "1.0.0",
                EnvelopeVersion = "v6",
                Attempts = 1,
                State = Gg.Contracts.FlightStates.Landed,
                Facts = [],
            },
        ],
    };

    [Test]
    public async Task Every_modal_is_reachable_from_a_fresh_console()
    {
        // A modal nobody can open cannot trap anybody, so the property above
        // would hold trivially for a mode that is simply unreachable. This is
        // the other half: each one can genuinely be entered.
        // WITH ONE FLIGHT IN IT, and that is a widening of "fresh" rather than
        // an exemption. `enter` opens the flight under the cursor and a console
        // with no flights has none - Article XI, a key that appears to work is
        // worse than one that is not offered - so pressing keys against a
        // console that has loaded nothing could never reach that modal. What
        // the walk is about is whether a KEY can open each one, and a list with
        // a row in it is the smallest state where that question is meaningful.
        var loaded = new AppState { Flights = OneFlight() };

        foreach (var mode in Enum.GetValues<UiMode>()
                     .Where(m => m != UiMode.Normal && !OpenedByTheLoop.ContainsKey(m)))
        {
            var opened = KeymapTests.Universe
                .Select(key => Press(loaded, key))
                .Any(state => state.Mode == mode);

            await Assert.That(opened).IsTrue().Because($"{mode} cannot be opened by any key.");
        }
    }

    [Test]
    public async Task The_exemption_list_names_nothing_a_key_can_open()
    {
        // THE ROW THAT KEEPS THE LIST HONEST. An exemption that stopped being
        // needed is a hole nobody is looking at, and this is the shape
        // ConsoleDataReachTests already uses for the same hazard.
        var stale = OpenedByTheLoop.Keys
            .Where(mode => KeymapTests.Universe
                .Select(key => Press(new AppState(), key))
                .Any(state => state.Mode == mode))
            .ToList();

        await Assert.That(stale).IsEmpty()
            .Because("a mode a key can open does not need excusing, and an excuse that is "
                   + "not needed is a hole in the walk above.");
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
            var state = StateGenerator.Next(random) with { ActiveTab = TabId.Queue };

            for (var step = 0; step < random.Next(1, MaxLength); step++)
            {
                state = Press(state, KeymapTests.Universe[random.Next(KeymapTests.Universe.Count)]);

                // THE SAME CLAIM, TWICE MOVED. It was "focus is on a visible
                // pane"; then a view took the whole screen and it became "the
                // tab showing is one somebody opened"; and now every tab is on
                // the bar, so being open is not a thing a tab can fail to be.
                // What is still worth asserting over generated keys is that
                // exactly one view is ever drawn - the invariant the whole
                // layout rests on.
                var drawn = Tabs.All.Count(tab => Tabs.Showing(state, tab));

                await Assert.That(drawn).IsEqualTo(1)
                    .Because($"seed {seed} draws {drawn} views at once.");
            }
        }
    }
}
