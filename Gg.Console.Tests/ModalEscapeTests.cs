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
        // THE SAME DERIVATION THE SCREEN USES, which this did not have. It
        // built a context from three fields by hand, so every flag derived
        // from the model - whose runner is under the cursor, whether its
        // allowance is yours - was false here however the model was set up.
        // That is the third-copy hazard the remark below names, in the line
        // above it: a modal reachable only through a derived flag read as
        // unreachable, and the walk could not tell that from a missing key.
        var command = Keymap.Resolve(key, KeymapContext.For(state));

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

        [UiMode.ConfirmGround] =
            "opened by `x` inside the flight modal, which is itself opened by the loop after "
          + "a read - so it is two steps from a fresh state and this test presses one key. "
          + "It exists because `x` used to ground on that one keypress: the session ended and "
          + "an editor opened for a reason before anybody had agreed to anything, and the "
          + "only way back out was to write nothing and read the refusal. "
          + "GroundingIsOfferedOnlyWhereTheFlightIsTests holds the chain - asked in the modal "
          + "that names the flight, answered in the question that modal opens, and neither "
          + "anywhere else - and escapability is covered above, because StateGenerator emits "
          + "every UiMode.",

        [UiMode.ConfirmFlyAgain] =
            "opened by `f` inside the flight modal, for the reason above and by the same two "
          + "steps. Answering it opens the editor on this flight's own intent rather than "
          + "flying anything, so the real decision lands where a person can still change it - "
          + "and abandoning the editor opens nothing either.",

        [UiMode.ConfirmRetire] =
            "opened by `x' INSIDE the changeset view, which `v' then `d' reach - so it is "
          + "three keys from a fresh state and this test presses one. It is in a modal on "
          + "purpose: the names it retires are the ones whose files are gone, so there is no "
          + "tree row to point at, and `x' is forget-credential in Normal mode with the "
          + "Envelope tab's spread declared earlier - a tab-scoped one would have shadowed "
          + "it without saying so. RetiringIsReachableFromTheChangesetTests holds the chain "
          + "and asserts the Normal-mode binding survives; escapability is covered above, "
          + "because StateGenerator emits every UiMode.",

        [UiMode.ReadingEnvelope] =
            "opened by `e' from either of the other two reading views, which `o' reaches - "
          + "so it is two keys deep and this test presses one. `v' used to open it from the "
          + "tab and now turns the pane beside the tree, which is where a document is read; "
          + "the FLOOR is readable there too, as root.yaml's applied view, so this modal "
          + "view is a convenience rather than the only route to it. "
          + "TheAirspaceKeysWalkEndToEndTests walks o then e and back. Escapability is "
          + "covered above, because StateGenerator emits every UiMode.",

        [UiMode.ReadingOutcome] =
            "opened BY THE LOOP after an apply returns - Reducer.ApplyAnswered, called with "
          + "the terminal already back - so no key opens it from a fresh state and none "
          + "should: a person who just pressed `y` is owed the answer without discovering a "
          + "second keystroke. It is also reachable by `o` from inside either other reading "
          + "view, which is two keys deep and this test presses one. "
          + "TheApplyOutcomeIsReadableTests holds both routes: that the loop's arm opens it "
          + "when there is an outcome and opens nothing when there is not, and that `o` "
          + "comes back to it from the diff. Escapability is covered above, because "
          + "StateGenerator emits every UiMode.",

        [UiMode.ReadingChangeset] =
            "opened by `d` inside the reading modal, which `v` opens - so it is two steps "
          + "from a fresh state and this test presses one key. It is two steps ON PURPOSE: "
          + "`d` is `decide` in Normal mode and a tab-scoped one would never fire, because "
          + "Keymap.Resolve answers the first match and Normal's is declared earlier - so a "
          + "second key for the second view would have had to spend one of the four letters "
          + "left, and inside a modal the letters are free. "
          + "TheChangesetIsReadableTests.The_two_views_are_reachable_from_each_other holds "
          + "the chain in both directions, and escapability is covered above, because "
          + "StateGenerator emits every UiMode.",

        [UiMode.HandFlight] =
            "opened by ConsoleLoop's FlyByHand arm, and only when nothing was created. "
          + "Whether it opens depends on a read the loop makes with the terminal released - "
          + "whether this machine advertises what the flight needs - so no key can decide "
          + "it. Entered through Reducer.HandFlightAnswered and left through CloseModal; "
          + "TheHandFlightRefusalIsAModalTests drives the real loop into it, and asserts a "
          + "flight that flew opens nothing.",
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
        // AND FROM EVERY TAB, because enter means the row under the cursor and
        // which row that is depends on which tab has the screen. One fixed tab
        // made this a walk over the keys of one pane, which would have called
        // the runner modal unreachable while a key opened it.
        //
        // AND WITH A MACHINE AND AN ALLOWANCE THIS PERSON OWNS, for the reason
        // one paragraph up applied to a second key. `o` is live only on your
        // own machine's allowance, so a fleet with no runners leaves
        // FloorChoice unreachable - not because no key opens it, but because
        // the walk never built the state where that key exists.
        var everywhere = Enum.GetValues<TabId>()
            .Select(tab => new AppState
            {
                Flights = OneFlight(),
                ActiveTab = tab,
                PrincipalId = "me",
                RunnerSelected = 0,
                Runners = new Gg.Contracts.RunnerList
                {
                    Runners =
                    [
                        new()
                        {
                            RunnerId = "01a078bb-4b97-779b-81ff-554c4ea662c0",
                            Label = "a-laptop",
                            State = Gg.Contracts.RunnerStates.Idle,
                            RegisteredByPrincipalId = "me",
                        },
                    ],
                },
                Allowances = new Gg.Contracts.AllowanceList
                {
                    Allowances =
                    [
                        new()
                        {
                            Name = "an-allowance",
                            MeasuredAt = DateTimeOffset.UnixEpoch,
                            Runners = ["01a078bb-4b97-779b-81ff-554c4ea662c0"],
                            Windows = [],
                        },
                    ],
                },
            })
            .ToList();

        // TWO KEYS, NOT ONE, because a modal may live behind another. The
        // airspace tab's four acts collapsed behind `a' when its status line
        // outgrew the terminal, so `s' opens the apply question from INSIDE
        // that modal and a one-press walk called it unreachable. The claim
        // being made is "a person can get here from a console they just
        // opened", and two keystrokes is still that.
        var reachable = everywhere
            .Concat(from loaded in everywhere
                    from key in KeymapTests.Universe
                    select Press(loaded, key))
            .ToList();

        var reached = (from loaded in reachable
                       from key in KeymapTests.Universe
                       select Press(loaded, key).Mode)
            .ToHashSet();

        foreach (var mode in Enum.GetValues<UiMode>()
                     .Where(m => m != UiMode.Normal && !OpenedByTheLoop.ContainsKey(m)))
        {
            await Assert.That(reached.Contains(mode)).IsTrue()
                .Because($"{mode} cannot be opened by any two keys.");
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
