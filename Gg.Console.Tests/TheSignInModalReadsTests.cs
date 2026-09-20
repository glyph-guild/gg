namespace Gg.Console.Tests;

/// <summary>
/// What the sign-in modal puts in front of a person, at each step.
/// </summary>
/// <remarks>
/// <para>
/// <b>A modal with no text is a bordered box.</b> <c>PaneText.Modal</c>
/// dispatches on the mode and returns <c>""</c> for anything it does not know,
/// so a mode can be added, own the keyboard, and draw nothing at all — which
/// looks exactly like the console having frozen.
/// </para>
/// <para>
/// <b>And the hint line is half of it.</b> The keys are advertised from the
/// context the screen builds, so a step whose context is not derived from the
/// model shows the other step's keys — the person reads <c>y sign in</c> under
/// a code they were asked to approve, presses it, and nothing happens.
/// </para>
/// </remarks>
public class TheSignInModalReadsTests
{
    private static readonly DateTimeOffset Expiry =
        new(2026, 9, 6, 14, 32, 0, TimeSpan.Zero);

    /// <summary>Written as escapes, because a raw one in source is invisible.</summary>
    private const string Esc = "\u001b";

    private const string Bel = "\u0007";

    private static AppState Offered() => new() { Mode = UiMode.SignIn };

    private static AppState Started() => Offered() with
    {
        SignIn = new PendingSignIn
        {
            UserCode = "WDJB-MJHT",
            VerificationUri = "https://example.test/device",
            ExpiresAt = Expiry,
        },
    };

    [Test]
    public async Task The_offer_says_why_the_console_behind_it_is_empty()
    {
        // The queue says "nothing needs you" whether nothing needs you or
        // nobody could ask, and this modal is drawn over the second one. A
        // person who reads only the pane behind it learns the opposite of the
        // truth.
        var text = PaneText.Modal(Offered());

        await Assert.That(text).IsNotEmpty()
            .Because("a modal that owns the keyboard and draws nothing is indistinguishable "
                   + "from a console that has frozen.");
        await Assert.That(text).Contains("signed in");
    }

    [Test]
    public async Task Where_to_go_and_what_to_type_are_both_on_the_screen()
    {
        // The whole reason the shell fetches the code before waiting on it. A
        // device flow whose code is only ever printed to a terminal the console
        // then redraws over is a device flow nobody can complete.
        var text = PaneText.Modal(Started());

        await Assert.That(text).Contains("https://example.test/device");
        await Assert.That(text).Contains("WDJB-MJHT");
    }

    [Test]
    public async Task A_code_says_when_it_stops_working()
    {
        // A code with no expiry on it is one somebody comes back to after lunch
        // and concludes the product is broken.
        await Assert.That(PaneText.Modal(Started())).Contains("14:32");
    }

    [Test]
    public async Task What_the_last_attempt_said_is_where_it_will_be_read()
    {
        // Expired, declined, or pressed a moment early — the arm returns to the
        // offer, and the offer is the only thing on the screen. A reason
        // recorded on the model and drawn nowhere is a key that appears to have
        // done nothing.
        var text = PaneText.Modal(
            Offered() with { LastSignIn = "That code expired before it was approved." });

        await Assert.That(text).Contains("That code expired before it was approved.");
    }

    [Test]
    public async Task The_offer_names_no_command_to_type_in_a_terminal_it_has_taken()
    {
        // THE DEFECT THIS WHOLE SLICE IS ABOUT, asserted so it cannot come back
        // as a helpful addition. "Run gg login" was true, and the person could
        // not do it: gg had the terminal.
        await Assert.That(PaneText.Modal(Offered())).DoesNotContain("gg login");
        await Assert.That(PaneText.Modal(Started())).DoesNotContain("gg login");
    }

    [Test]
    public async Task The_renderer_is_still_the_last_line_of_defence()
    {
        // Text is stored clean, so in a healthy system this removes nothing -
        // and PaneText cleans anyway, because it is the last code between a
        // control plane and a screen that ACTS on escape sequences. Every other
        // pane here does it; a new one that did not would be the gap.
        var text = PaneText.Modal(Offered() with
        {
            SignIn = new PendingSignIn
            {
                UserCode = Esc + "[2JWDJB-MJHT",
                VerificationUri = "https://example.test/device" + Esc + "]0;owned" + Bel,
                ExpiresAt = Expiry,
            },
            LastSignIn = "Signed in as " + Esc + "[31msomebody.",
        });

        await Assert.That(text).DoesNotContain(Esc);
        await Assert.That(text).Contains("WDJB-MJHT")
            .Because("what is removed is the sequence, never the code somebody has to type.");
    }

    [Test]
    public async Task The_keys_offered_are_derived_from_the_model()
    {
        // Both steps live in one mode, so the mode alone cannot say which keys
        // are live. A context built by hand beside the model is a second place
        // to remember, and this one has already been forgotten twice.
        await Assert.That(KeymapContext.For(Started()).SignInStarted).IsTrue();
        await Assert.That(KeymapContext.For(Offered()).SignInStarted).IsFalse();

        var live = Keymap.Bindings(KeymapContext.For(Started())).Select(b => b.Key).ToList();

        // Keyed on opening the link rather than on signing in: approving is no
        // longer a key at all, so the command that used to prove this context
        // was live has nothing bound to it once a code is showing.
        await Assert.That(live).Contains(
            Keymap.Bindings(new KeymapContext(UiMode.SignIn) { SignInStarted = true })
                .Single(b => b.Command == Command.OpenSignInUri).Key);
    }

    [Test]
    public async Task The_screen_derives_its_context_rather_than_rebuilding_one()
    {
        // ShellHandledTests' shape, for the same class of defect one type over.
        // The screen's hand-built context has dropped THREE members already -
        // Takeable, HandedBackable and RepositoriesVisible - so the help page
        // advertises keys the screen would not resolve and a hint reads "show"
        // for a pane that is already showing. Neither is visible today because
        // nothing in production sets the first two; both become visible the day
        // something does, which is the worst time to find out.
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("KeymapContext.For(State)")
            .Because("one derivation, read by the screen, the help page and these tests, is "
                   + "what stops the advertised keys and the live ones drifting.");
    }

    [Test]
    public async Task The_derivation_reads_every_field_the_keymap_dispatches_on()
    {
        // The ratchet on the derivation itself. A member added to KeymapContext
        // and never read off the model is a binding that can only ever be
        // reached by a test - which is how three of them got here.
        var state = new AppState
        {
            Mode = UiMode.Normal,
            LiveVisible = true,
            Frozen = true,
            BrowseVisible = true,
            EnvelopeVisible = true,
            RepositoriesVisible = true,
            TakeableTree = "/somewhere",
            TakenOver = true,
            SignIn = Started().SignIn,

            // A FLEET WITH THIS MACHINE'S RUNNER UNDER THE CURSOR, because
            // RunnerIsOurs is derived from the selected ROW rather than from a
            // flag on the model - there is nothing to set true directly, and a
            // model without a fleet leaves it false however the derivation is
            // written.
            //
            // AND THAT RUNNER IS BEATING, for the same reason and a second
            // flag: watching is offered only over a machine that can pick an
            // introduction up, so a row with no beat leaves RunnerIsBeating
            // false and this assertion cannot tell that from a derivation that
            // never reads it.
            Machine = "a-laptop",
            LocalRunnerId = "01a078bb-4b97-779b-81ff-554c4ea662c0",
            RunnerSelected = 0,
            Runners = new Gg.Contracts.RunnerList
            {
                Runners =
                [
                    new()
                    {
                        RunnerId = "01a078bb-4b97-779b-81ff-554c4ea662c0",
                        Label = "a-laptop",
                        State = Gg.Contracts.RunnerStates.Busy,
                        CurrentFlightNumber = "GG-1",

                        // REGISTERED BY THIS PERSON, which is what makes the
                        // allowance theirs to reserve. Yours is a fact the
                        // control plane recorded, where Mine is this console's
                        // own inference about one machine - so both have to be
                        // true here and they are set from different places.
                        RegisteredByPrincipalId = "01a078bb-4b97-779b-81ff-554c4ea662c1",

                        // AND CLAIMED BY THEM, AND RESERVED, which is three
                        // more facts and not one: the control plane says whose
                        // this machine is at all, it says this person, and it
                        // says the machine takes only their flights. Each is a
                        // flag the runner modal's ownership keys branch on, and
                        // a model that left any of them false would leave those
                        // keys outside the completeness check - the mistake
                        // this file exists to catch.
                        Ownership = Gg.Contracts.RunnerOwnerships.Claimed,
                        Owner = "the owner",
                        OwnerPrincipalId = "01a078bb-4b97-779b-81ff-554c4ea662c1",
                        Reserved = true,
                    },
                ],
            },

            PrincipalId = "01a078bb-4b97-779b-81ff-554c4ea662c1",

            // AND BOTH GATES ON THE FLEET PANE, which is one flag on the
            // context and two on the model - the control plane's answer about
            // this person and the file's answer about this console.
            IsAdmin = true,
            FleetAllowancesShown = true,

            // AND AN ALLOWANCE THAT MACHINE REPORTS. AllowanceIsMine needs a
            // reported allowance as well as ownership: there is no floor to
            // set on a machine that spends from nothing anybody named.
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

            // AND A CURSOR ON A DOCUMENT, which is what makes `v' mean "read
            // this one back" rather than "read the rules in force". The flag
            // is derived from the tree and the cursor together, so both have
            // to be here - a model with a tree and no row would leave it false
            // and this guard would call the derivation blind.
            Estate = new EstateOnThisMachine
            {
                Root = "/home/someone/airspace",
                Uncommitted = [],
                Tree = new WorkingCopy
                {
                    Present = true,
                    Documents = [new("root", "root", "airspace/root.yaml", "v6")],
                    Unreadable = [],
                },
            },
            // ROW ONE, NOT ROW ZERO. Row zero is the synthesised `airspace/'
            // folder, and a folder row holds no document - which is the
            // projection working, and worth pinning here because it is the
            // difference between this flag being derived and being guessed.
            AirspaceSelected = 1,

            // AND THE KEYBOARD IN THE DOCUMENT, which is what makes `w' say
            // "back to the tree" rather than "read the document". Same
            // argument as the cursor above: a flag the derivation does not
            // read is a hint line that can advertise the wrong direction.
            AirspaceReading = true,

            // ON A GROUP, so the fold key's flag is exercised like the rest.
            HelpFold = UiMode.Help,

            // AND ON THE LOOK PAGE, for that reason: a flag the derivation
            // forgets to read is a key the keymap offers and the hint line
            // never advertises, or the reverse.
            //
            // THE MODE ON THIS MODEL IS Normal, DELIBERATELY - it is one state
            // with every flag raised, not a state anybody could be in. That is
            // what caught the first version of this flag: it was derived as
            // "Help mode AND the Look page", which no such model can satisfy.
            // Resolve already dispatches on Mode, so asking again in the
            // derivation was a second answer to a settled question.
            HelpPage = HelpPage.Look,

            // AND THE COMPOSE MODAL'S SECOND HALF, for that same reason.
            WorkKindTab = WorkKindTab.Repositories,

            // AND A FLIGHT NAMING A TICKET THIS MACHINE HAS A READER FOR, which
            // is two facts from two places for one flag - the flight's intent,
            // which the control plane recorded, and the declarations this
            // console was started with. A model carrying one of them leaves the
            // flag false however the derivation is written, which is the trap
            // RunnerIsOurs set above.
            ReaderKeys = ["a-tracker"],

            // AND A GATE WAITING ON THE ROW UNDER THE QUEUE'S CURSOR, which is
            // again two facts from two places: the queue row, and the gate list
            // holding one for that flight. A model with the row and no gate
            // leaves the flag false however the derivation is written.
            Queue =
            [
                new QueueRow
                {
                    FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
                    FlightNumber = "GG-118", Key = "019fe815-6136-7518-bb57-b06d6d3f411a", Reference = "GG-118",
                    Name = "the login form loses focus",
                    Reason = QueueReason.AwaitingDecision,
                    Since = DateTimeOffset.UnixEpoch,
                },
            ],
            SelectedRow = 0,
            Gates = new Gg.Contracts.GateList
            {
                Gates =
                [
                    new Gg.Contracts.PendingGate
                    {
                        FlightNumber = "GG-118",
                        ObligationId = "a-human-reviews-it",
                        Approver = "somebody",
                        Because = "a person reviews what the agent wrote",
                        AwaitingSince = DateTimeOffset.UnixEpoch,
                        Attempt = 1,
                        ManifestHash = "sha256:0000",

                        // AND WHAT IT IS ASKING FOR, because the gate modal
                        // binds a key only where there is one to act on. This
                        // model sets one of everything, so a gate with no
                        // maintenance ask would leave GateAsksForAgentLogin
                        // false and the completeness check below would call
                        // the derivation unread.
                        Maintenance = new Gg.Contracts.GateMaintenance
                        {
                            Kind = Gg.Contracts.GateMaintenanceKinds.AgentLogin,
                            Runner = "019fe8a2-0707-70c2-9ff8-be3adb54cef0",
                            RunnerLabel = "somebody's laptop",
                            Provider = "claude",
                        },
                    },
                ],
            },
            // AND A STANDING NOMINATION UNDER THE BOARD'S CURSOR, which is two
            // facts again: the board holds a row that has not ended, and the
            // cursor is on it. A board whose only row is a watch, or whose one
            // nomination has already been answered, leaves the flag false
            // however the derivation is written.
            Board = new Gg.Contracts.BoardPage
            {
                IncludedEnded = true,
                Nominations =
                [
                    new()
                    {
                        NominationId = new Guid("019fe8b4-0000-7000-8000-00000000000b"),
                        Nominator = "watch:nightly-triage",
                        Subject = "work-item:https://tracker.example/acme/4242",
                        Version = "7",
                        WorkKind = "review",
                        Mode = "gated",
                        State = "standing",
                        MadeAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
            BoardSelected = 0,

            // AND AN ACTIVITY LINE SHOWING PART OF ITSELF, which is two facts
            // as well: something was said, and the line it landed on is
            // narrower than it is. Either alone leaves the flag false however
            // the derivation is written - a wide terminal clips nothing, and an
            // empty line has nothing to clip.
            LastAction = "the runner refused the credential because its own configuration "
                       + "does not say accept-configured",
            SaidColumns = 40,
            Watches = new Gg.Contracts.WatchStandingList { Standings = [] },
            Flights = new Gg.Contracts.FlightList
            {
                Flights =
                [
                    new()
                    {
                        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
                        FlightNumber = "GG-118",
                        Name = "the login form loses focus",
                        Intent = new Gg.Contracts.FlightIntent
                        {
                            Kind = Gg.Contracts.FlightIntentKinds.Ticket,
                            Provider = "a-tracker",
                            Id = "17864",
                        },
                        CreatedAt = DateTimeOffset.UnixEpoch,
                        RunnerProtocolVersion = 1,
                        FactVocabularyVersion = "0.31.0",
                        ConstitutionVersion = "1.0.0",
                        EnvelopeVersion = "v7",
                        Attempts = 1,
                        State = Gg.Contracts.FlightStates.Open,
                        Facts = [],
                    },
                ],
            },
        };

        var context = KeymapContext.For(state);

        // THE ONE PAIR THAT CANNOT BOTH BE TRUE, named rather than skipped. An
        // intent has exactly one kind, so a flight that names a ticket a reader
        // here can read is not a flight that names a bare link - and the key
        // they share means the modal in the first case and the browser in the
        // second, which is the whole point of the second flag. This model keeps
        // the ticket, because that is the arm with the reader in it; the link
        // arm has a class of its own where both are set the other way.
        var exclusive = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // THE SECOND PAIR THAT CANNOT BOTH BE TRUE. A machine is the
            // tenant's or somebody's claim, never both - the doors refuse it -
            // and this model keeps the claim, because that is the arm with the
            // two toggles in it. The tenant arm is where the admin's key reads
            // the other way, and AnAdminsWordAsksFirstTests holds it.
            // AND THE THIRD. A gate asks for one kind of maintenance: a
            // machine's agent cannot start, or a machine does not meet its
            // profile. This model keeps the agent login, because that is the
            // arm with an act on it; TheConsoleAnswersABringUpGateTests holds
            // the other, where both answers are withheld instead.
            [nameof(KeymapContext.GateIsABringUpAsk)] =
                "a gate asks for one kind of maintenance, never both - "
              + "TheConsoleAnswersABringUpGateTests holds the other arm.",

            [nameof(KeymapContext.RunnerIsTheTenants)] =
                "a machine is the tenant's or claimed, never both - "
              + "AnAdminsWordAsksFirstTests holds the other arm.",

            [nameof(KeymapContext.OverALink)] =
                "a flight's intent is a ticket or a link, never both - "
              + "AFlightCanOpenItsLinkTests holds the other arm.",
        };

        var defaults = typeof(KeymapContext)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool))
            .Where(p => !(bool)p.GetValue(context)!)
            .Select(p => p.Name)
            .Where(name => !exclusive.ContainsKey(name))
            .ToList();

        await Assert.That(defaults).IsEmpty()
            .Because("every flag the keymap dispatches on is set on this model, so one still "
                   + "false is one the derivation does not read. Found: "
                   + string.Join(", ", defaults));

        // AND THE EXEMPTION STAYS HONEST. A flag listed here that this model
        // DOES set is one somebody exempted and then made reachable, which
        // would quietly take it out of the check above for ever.
        foreach (var (flag, why) in exclusive)
        {
            await Assert.That((bool)typeof(KeymapContext).GetProperty(flag)!.GetValue(context)!)
                .IsFalse()
                .Because($"{flag} is exempt because {why}");
        }
    }
}
