using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The queue answers the nominations it lists, one at a time or marked
/// together, and a watch's failure reads as one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on the dev tenant, 2026-10-05: 75 rows reading "sweep · nominated ·
/// open it?" and no key that answered any of them.</b> Every one was
/// <c>jdx-triage</c> reporting that it could not sweep - one row per failed
/// hour, by design, because three failures are three things a person deciding
/// "credential or outage" needs to see. The queue drew each as a request to
/// start work, and enter opened the FLIGHT actions over a row that has no
/// flight. The answers lived one tab over, in the board's modal.
/// </para>
/// <para>
/// <b>Three things, and none of them a new door.</b> A failure is named as one;
/// enter on a nomination opens the board's own question about it; and rows can
/// be marked and declined together with ONE sentence - which is still the
/// sentence the door demands, written once rather than seventy-five times.
/// </para>
/// </remarks>
public class TheQueueAnswersNominationsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 19, 0, 0, TimeSpan.Zero);

    private static readonly Guid Work = new("01a0792a-5e1f-7030-a5d8-52fd66e510b0");
    private static readonly Guid FailedOnce = new("cf4d714f-8ad5-748a-ad33-f39243657e26");
    private static readonly Guid FailedTwice = new("01f49127-2ea4-7a29-ad3c-db03e97e50f0");
    private static readonly Guid WentQuiet = new("b95ea939-e95b-75f1-8ad6-e1ab59341b5a");

    private static NominationSummary ANomination(
        Guid id, string subject, string workKind, string nominator, int minutesOld) => new()
    {
        NominationId = id,
        Nominator = nominator,
        Subject = subject,
        Version = id.ToString(),
        WorkKind = workKind,
        Mode = DestinationOpening.Gated,
        State = NominationStates.Standing,
        MadeAt = T0.AddMinutes(-minutesOld),
    };

    private static NominationSummary RealWork() => ANomination(
        Work, "work-item:https://tracker.example/acme/4242", "implement",
        "flight:01a0edc2-ecc0-767c-9fc7-9bdae5c48109", 300);

    private static NominationSummary SweepFailed(Guid id, int minutesOld) => ANomination(
        id, WatchFailures.Unreachable + "jdx-triage", "sweep", "watch:jdx-triage", minutesOld);

    private static NominationSummary Quiet() => ANomination(
        WentQuiet, WatchFailures.Missed + "jdx-triage", "sweep", "watch:jdx-triage", 30);

    private static BoardPage ABoard(params NominationSummary[] rows) => new()
    {
        Nominations = rows,
        IncludedEnded = false,
    };

    private static IReadOnlyList<QueueRow> QueueOf(BoardPage board) =>
        ConsoleProjection.Queue(
            new FlightList { Flights = [] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal),
            new RunnerList { Runners = [] },
            gates: null,
            board: board);

    /// <summary>The queue tab over these rows, cursor on the one named.</summary>
    private static AppState Queue(Guid? under = null, params NominationSummary[] rows)
    {
        var board = ABoard(rows.Length > 0
            ? rows
            : [RealWork(), SweepFailed(FailedOnce, 60), SweepFailed(FailedTwice, 120), Quiet()]);
        var queue = QueueOf(board);
        var at = under is { } id ? queue.ToList().FindIndex(r => r.NominationId == id) : 0;

        // NO BOARD, which is the console as booted: the board tab's page is
        // read only once somebody opens that tab. The first build passed every
        // test here with a board in this fixture and did nothing on a real one.
        return new AppState
        {
            ActiveTab = TabId.Queue,
            Standing = board,
            Queue = queue,
            SelectedRow = Math.Max(at, 0),
        };
    }

    private static Guid?[] Marked(AppState state) =>
        [.. state.Marked.Select(g => (Guid?)g).OrderBy(g => g)];

    // ---- a failure reads as one -------------------------------------------

    [Test]
    public async Task A_sweep_that_failed_is_named_for_its_watch_and_says_it_failed()
    {
        var row = QueueOf(ABoard(SweepFailed(FailedOnce, 60)))[0];

        await Assert.That(row.Reason).IsEqualTo(QueueReason.WatchFailing)
            .Because("it is not work somebody nominated, and 'nominated · open it?' sent a "
                   + "person looking for something to open on a row whose only content is "
                   + "that a credential broke.");
        await Assert.That(row.Name).IsEqualTo("jdx-triage · sweep failed")
            .Because("the watch is what somebody goes and fixes; 'sweep' alone named the "
                   + "work kind every one of these rows shares.");
    }

    [Test]
    public async Task A_watch_gone_quiet_is_named_differently_from_one_that_failed()
    {
        var row = QueueOf(ABoard(Quiet()))[0];

        await Assert.That(row.Reason).IsEqualTo(QueueReason.WatchFailing);
        await Assert.That(row.Name).IsEqualTo("jdx-triage · not reporting")
            .Because("a sweep that failed said why; a watch nothing is sweeping said "
                   + "nothing, and the fix for each is in a different place.");
    }

    [Test]
    public async Task Real_work_is_still_a_nomination()
    {
        var row = QueueOf(ABoard(RealWork()))[0];

        await Assert.That(row.Reason).IsEqualTo(QueueReason.NominationStanding);
        await Assert.That(row.Name).IsEqualTo("implement");
    }

    [Test]
    public async Task The_reason_column_says_the_watch_is_failing()
    {
        await Assert.That(PaneText.Reason(QueueReason.WatchFailing)).IsEqualTo("watch failing");
    }

    // ---- enter answers the row it is on ------------------------------------

    [Test]
    public async Task Enter_on_a_nomination_opens_the_boards_question_about_it()
    {
        var state = Queue(under: FailedTwice);

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(state)))
            .IsEqualTo(Command.ShowQueueNomination)
            .Because("the flight actions have nothing to say about a row with no flight - "
                   + "the open/decline question already exists, in the board's modal.");

        var opened = Reducer.Reduce(state, Command.ShowQueueNomination);

        await Assert.That(opened.Mode).IsEqualTo(UiMode.BoardDetail);
        await Assert.That(Rows.StandingUnder(opened)?.NominationId).IsEqualTo(FailedTwice)
            .Because("the modal's `d` declines whatever row it is about, so from the queue "
                   + "that has to be the queue's row - or `d` declines a row nobody is "
                   + "looking at.");
        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), KeymapContext.For(opened)))
            .IsEqualTo(Command.DeclineNomination);
        await Assert.That(PaneText.Modal(opened)).Contains("jdx-triage")
            .Because("the modal has to be about the row, not a box saying there is none.");
    }

    [Test]
    public async Task The_question_is_about_the_queues_row_even_with_a_board_tab_loaded()
    {
        // THE BOARD TAB'S PAGE, with its cursor on a different row. The
        // modal opened from the queue must not answer that one.
        var state = Queue(under: FailedTwice) with
        {
            Board = ABoard(RealWork()),
            BoardSelected = 0,
        };

        var opened = Reducer.Reduce(state, Command.ShowQueueNomination);

        await Assert.That(Rows.StandingUnder(opened)?.NominationId).IsEqualTo(FailedTwice);
    }

    [Test]
    public async Task Answering_from_the_queue_closes_the_question()
    {
        var opened = Reducer.Reduce(Queue(under: FailedTwice), Command.ShowQueueNomination);
        var actions = new ConsoleDoubles.Records();

        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.DeclineNomination),
                new ConsoleDoubles.Writes("the credential is fixed"),
                actions: actions)
            .Run(opened);

        await Assert.That(actions.Answered.Single().Nomination).IsEqualTo(FailedTwice.ToString());
        await Assert.That(final.Mode).IsEqualTo(UiMode.Normal)
            .Because("the answered row leaves the queue on the reload and the cursor lands on "
                   + "the next one; a modal left open would be asking about a row nobody "
                   + "chose, with both answers live on it.");
    }

    [Test]
    public async Task Enter_on_a_flight_still_opens_what_can_be_done()
    {
        var flight = new QueueRow
        {
            Key = "f-1",
            Reference = "GG-42",
            FlightId = "f-1",
            FlightNumber = "GG-42",
            Name = "a flight that stopped",
            Reason = QueueReason.AwaitingDecision,
            Since = T0,
        };
        var state = new AppState { ActiveTab = TabId.Queue, Queue = [flight] };

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(state)))
            .IsEqualTo(Command.ToggleFlightActions);
    }

    // ---- marking -----------------------------------------------------------

    [Test]
    public async Task Space_marks_the_nomination_under_the_cursor_and_again_unmarks_it()
    {
        var state = Queue(under: FailedOnce);

        await Assert.That(Keymap.Resolve(KeyStroke.Char(' '), KeymapContext.For(state)))
            .IsEqualTo(Command.ToggleMark);

        var marked = Reducer.Reduce(state, Command.ToggleMark);
        await Assert.That(Marked(marked)).IsEquivalentTo(new Guid?[] { FailedOnce });

        var unmarked = Reducer.Reduce(marked, Command.ToggleMark);
        await Assert.That(unmarked.Marked).IsEmpty();
    }

    [Test]
    public async Task The_line_names_space_as_space()
    {
        // FOUND BY WALKING IT: the hint line read "·   mark ·", because a key
        // was named by its character and this one's character is a blank.
        var hints = Keymap.Hints(KeymapContext.For(Queue(under: FailedOnce)));

        await Assert.That(hints).Contains("space mark");
    }

    [Test]
    public async Task Space_does_nothing_on_a_flight()
    {
        var flight = new QueueRow
        {
            Key = "f-1",
            Reference = "GG-42",
            FlightId = "f-1",
            FlightNumber = "GG-42",
            Name = "a flight that stopped",
            Reason = QueueReason.AwaitingDecision,
            Since = T0,
        };
        var state = new AppState { ActiveTab = TabId.Queue, Queue = [flight] };

        await Assert.That(Keymap.Resolve(KeyStroke.Char(' '), KeymapContext.For(state))).IsNull()
            .Because("a flight is answered through its gate, not declined - marking one would "
                   + "be a mark nothing can act on.");
    }

    [Test]
    public async Task Star_marks_every_watch_failure_and_leaves_real_work_alone()
    {
        var state = Queue();

        await Assert.That(Keymap.Resolve(KeyStroke.Char('*'), KeymapContext.For(state)))
            .IsEqualTo(Command.MarkFailures);

        var marked = Reducer.Reduce(state, Command.MarkFailures);

        await Assert.That(Marked(marked))
            .IsEquivalentTo(new Guid?[] { FailedOnce, FailedTwice, WentQuiet }.OrderBy(g => g))
            .Because("'clear all errors' is the ask, and real work in the same list is the "
                   + "row a bulk answer must never take with it.");
    }

    [Test]
    public async Task Star_again_unmarks_them()
    {
        var once = Reducer.Reduce(Queue(), Command.MarkFailures);
        var twice = Reducer.Reduce(once, Command.MarkFailures);

        await Assert.That(twice.Marked).IsEmpty();
    }

    [Test]
    public async Task Star_is_not_offered_when_no_watch_is_failing()
    {
        var state = Queue(under: Work, RealWork());

        await Assert.That(Keymap.Resolve(KeyStroke.Char('*'), KeymapContext.For(state))).IsNull();
    }

    [Test]
    public async Task With_rows_marked_d_declines_them_and_without_it_still_decides_a_gate()
    {
        var plain = Queue(under: FailedOnce);
        var marked = Reducer.Reduce(plain, Command.ToggleMark);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), KeymapContext.For(plain)))
            .IsEqualTo(Command.OpenGate);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), KeymapContext.For(marked)))
            .IsEqualTo(Command.DeclineMarked);
        await Assert.That(ShellCommands.Handled).Contains(Command.DeclineMarked)
            .Because("it posts and hands the terminal to $EDITOR first, so it happens between "
                   + "sessions - a pure reduction would decline nothing.");
    }

    [Test]
    public async Task A_marked_row_shows_its_mark()
    {
        var marked = Reducer.Reduce(Queue(under: FailedOnce), Command.ToggleMark);
        var lines = PaneText.QueueRows(marked);

        await Assert.That(lines.Single(l => l.Contains("cf4d714f", StringComparison.Ordinal)))
            .StartsWith("● ");
        await Assert.That(lines.Single(l => l.Contains("01f49127", StringComparison.Ordinal)))
            .StartsWith("  ")
            .Because("the columns have to stay where they were, so an unmarked row gives the "
                   + "mark's width back as space.");
    }

    // ---- declining what is marked ------------------------------------------

    private sealed class KeepsWhatItWasGiven : IEditorSession
    {
        internal List<string> Given { get; } = [];

        public string Edit(string initialText)
        {
            Given.Add(initialText);
            return initialText;
        }
    }

    [Test]
    public async Task Declining_the_marked_sends_one_sentence_for_every_one_of_them()
    {
        var actions = new ConsoleDoubles.Records();
        var marked = Reducer.Reduce(Queue(), Command.MarkFailures);

        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.DeclineMarked),
                new ConsoleDoubles.Writes("the credential is fixed; these were its alarms"),
                actions: actions)
            .Run(marked);

        await Assert.That(actions.Answered.Select(a => a.Nomination).OrderBy(n => n))
            .IsEquivalentTo(new[] { FailedOnce, FailedTwice, WentQuiet }
                .Select(g => g.ToString()).OrderBy(n => n));
        await Assert.That(actions.Answered.All(a => !a.Open)).IsTrue()
            .Because("a bulk answer is a decline and only a decline - opening seventy flights "
                   + "on one keypress is the thing the sentence exists to prevent.");
        await Assert.That(actions.Answered.Select(a => a.Reason).Distinct())
            .IsEquivalentTo(new[] { "the credential is fixed; these were its alarms" });
        await Assert.That(final.Marked).IsEmpty();
        await Assert.That(final.LastNomination).Contains("Declined 3 of 3");
    }

    [Test]
    public async Task Failures_alone_come_with_a_sentence_already_written()
    {
        var editor = new KeepsWhatItWasGiven();
        var marked = Reducer.Reduce(Queue(), Command.MarkFailures);

        _ = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.DeclineMarked),
                editor,
                actions: new ConsoleDoubles.Records())
            .Run(marked);

        await Assert.That(editor.Given.Single()).IsNotEmpty()
            .Because("clearing a watch's alarms is the same sentence every time, and saving "
                   + "it as written is still a decision somebody made in their editor.");
    }

    [Test]
    public async Task Real_work_among_the_marked_gets_no_sentence_written_for_it()
    {
        var editor = new KeepsWhatItWasGiven();
        var state = Queue(under: Work);
        var marked = Reducer.Reduce(
            Reducer.Reduce(state, Command.ToggleMark), Command.MarkFailures);
        var actions = new ConsoleDoubles.Records();

        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.DeclineMarked),
                editor,
                actions: actions)
            .Run(marked);

        await Assert.That(editor.Given.Single()).IsEmpty()
            .Because("declining somebody's nominated work is the decision the sentence is "
                   + "for, and a pre-written one would let it pass on a reflex.");
        await Assert.That(actions.Answered).IsEmpty()
            .Because("an empty sentence sends nothing, exactly as the one-row answer does.");
        await Assert.That(final.Marked).Count().IsEqualTo(4)
            .Because("nothing was declined, so nothing was unmarked.");
    }

    [Test]
    public async Task A_refusal_is_counted_and_its_row_stays_marked()
    {
        var marked = Reducer.Reduce(Queue(), Command.MarkFailures);

        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.DeclineMarked),
                new ConsoleDoubles.Writes("alarms"),
                actions: new ConsoleDoubles.Records(refusing: true))
            .Run(marked);

        await Assert.That(final.LastNomination).Contains("Declined 0 of 3");
        await Assert.That(final.LastNomination).Contains("could not be reached")
            .Because("the door's own sentence is the only thing anybody can act on.");
        await Assert.That(final.Marked).Count().IsEqualTo(3)
            .Because("a row the door refused is still standing, and pressing `d` again is how "
                   + "somebody retries it.");
    }
}
