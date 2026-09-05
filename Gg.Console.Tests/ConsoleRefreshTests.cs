using Gg.Local;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The console stops being a photograph taken at boot.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing refreshed, ever.</b> <c>ConsoleStart.LoadAsync</c> ran once,
/// before the loop, and there was no command that reloaded and no arm that
/// could. Answer a gate and it stayed in the list; open a flight and the queue
/// did not grow. The only feedback was a <c>Last*</c> sentence, which is a
/// receipt and not a state.
/// </para>
/// <para>
/// <b>Between sessions, which is where the writes already are.</b> Rule 3: no
/// I/O inside a UI session. A refresh is a shell command with an arm, exactly
/// like every write the console already does - the session ends, the loop
/// reloads, the next session renders the new model.
/// </para>
/// <para>
/// <b>And rule 4: every write refreshes what it invalidated.</b> A decision
/// answered has to leave the gate list, or the console is showing a person work
/// they have already done.
/// </para>
/// </remarks>
public class ConsoleRefreshTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    private static QueueRow Row(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = "waiting",
        Reason = QueueReason.AwaitingDecision,
        Since = T0,
    };

    /// <summary>A console with a browsed row under the cursor.</summary>
    private static AppState Browsing() =>
        Reducer.Browsed(
            Booted() with { BrowseVisible = true },
            "a-tracker",
            new BrowseOutcome.Listed(new WorkItemPage(
                [new WorkItemSummary("18398", "A draft job fails", "New", "", null)], null)));

    private static AppState Booted() => new()
    {
        Queue = [Row("a", 1)],
        Principal = "somebody",
        EvidenceVisible = true,
        SelectedRow = 0,
    };

    /// <summary>A session that presses one key and returns.</summary>
    private sealed class Presses(params Command[] keys) : IUiSession
    {
        private int _at;

        internal List<AppState> Rendered { get; } = [];

        public UiOutcome Run(AppState state)
        {
            Rendered.Add(state);

            return _at < keys.Length
                ? new UiOutcome(keys[_at++], state)
                : new UiOutcome(Command.Quit, state);
        }
    }

    /// <summary>A reload that answers with whatever it was given.</summary>
    private sealed class Reloads(AppState next)
    {
        internal int Calls { get; private set; }

        internal AppState Load(AppState _)
        {
            Calls++;
            return next;
        }
    }

    [Test]
    public async Task A_refresh_key_reloads_the_snapshot()
    {
        var fresh = Booted() with { Queue = [Row("a", 1), Row("b", 2)] };
        var reload = new Reloads(fresh);
        var ui = new Presses(Command.Refresh);

        var final = new ConsoleLoop(ui, new NoEditor(), reload: reload.Load).Run(Booted());

        await Assert.That(reload.Calls).IsEqualTo(1);
        await Assert.That(ui.Rendered[^1].Queue.Count).IsEqualTo(2)
            .Because("the NEXT session renders the reloaded model - that is what a refresh "
                   + "is, in a console whose sessions hand the terminal back.");
        await Assert.That(final.Queue.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_refresh_keeps_what_the_person_was_looking_at()
    {
        // THE VIEW IS NOT DATA. A reload answers with fresh flights, gates and
        // logs; which row somebody had highlighted and whether they had the
        // evidence pane open are theirs, and losing them on every refresh would
        // make the key not worth pressing.
        var fresh = Booted() with
        {
            Queue = [Row("a", 1), Row("b", 2)],
            EvidenceVisible = false,
            SelectedRow = 0,
        };
        var reload = new Reloads(fresh);
        var ui = new Presses(Command.Refresh);

        var final = new ConsoleLoop(ui, new NoEditor(), reload: reload.Load)
            .Run(Booted() with { SelectedRow = 0, EvidenceVisible = true });

        await Assert.That(final.EvidenceVisible).IsTrue()
            .Because("a pane the person opened stays open across a reload.");
    }

    [Test]
    public async Task A_refresh_that_fails_keeps_the_last_good_model_and_says_so()
    {
        // S28.3-04. Emptying the screen because a read failed is the worst
        // answer: the person loses what they had AND cannot tell whether the
        // work went away.
        var reload = new Throws();
        var ui = new Presses(Command.Refresh);

        var final = new ConsoleLoop(ui, new NoEditor(), reload: reload.Load).Run(Booted());

        await Assert.That(final.Queue.Count).IsEqualTo(1)
            .Because("what was on the screen is still true until something better is known.");
        await Assert.That(final.Diagnosis).IsNotNull()
            .Because("and the person is told the screen is older than they think - a stale "
                   + "model nobody flagged is worse than an empty one.");
    }

    [Test]
    public async Task Answering_a_gate_refreshes_what_it_invalidated()
    {
        // RULE 4, and the most visible staleness today: approve, and the gate
        // stays in the list because nothing reloaded.
        var reload = new Reloads(Booted() with { Queue = [] });
        var ui = new Presses(Command.ApproveGate);

        var final = new ConsoleLoop(
            ui, new NoEditor(), actions: new Answers(), reload: reload.Load).Run(Booted());

        await Assert.That(reload.Calls).IsEqualTo(1)
            .Because("a decision changes what is waiting, so what is waiting is re-read.");
        await Assert.That(final.Queue).IsEmpty()
            .Because("the answered gate leaves the list, which is the whole point of "
                   + "answering it.");
    }

    [Test]
    public async Task Opening_a_flight_refreshes_the_queue()
    {
        var reload = new Reloads(Booted() with { Queue = [Row("a", 1), Row("b", 2)] });
        var ui = new Presses(Command.OpenFlight);

        new ConsoleLoop(
            ui, new SomethingTyped(), actions: new Answers(), reload: reload.Load)
            .Run(Booted());

        await Assert.That(reload.Calls).IsEqualTo(1)
            .Because("a flight opened is a flight the queue does not have yet.");
    }

    [Test]
    public async Task A_console_with_no_reload_says_so_rather_than_doing_nothing()
    {
        // THE DEAD-KEY SHAPE THIS ESTATE KEEPS FINDING. A bound key that
        // resolves, reaches the arm and returns the state unchanged is what
        // ShellHandledTests exists to prevent; a console built without a reload
        // says which it is.
        var ui = new Presses(Command.Refresh);

        var final = new ConsoleLoop(ui, new NoEditor()).Run(Booted());

        await Assert.That(final.Diagnosis).IsNotNull()
            .Because("configured without a reload is a real state, and silence about it is "
                   + "the failure mode this console has hit four times.");
    }

    // ---- S29.5-04, wired here at slice twenty-nine's author's request ----

    [Test]
    public async Task A_flight_opened_from_a_browsed_row_grows_the_queue()
    {
        var reload = new Reloads(Booted() with { Queue = [Row("a", 1), Row("b", 2)] });
        var ui = new Presses(Command.FlyPicked);

        new ConsoleLoop(ui, new NoEditor(), actions: new Answers(), reload: reload.Load)
            .Run(Browsing());

        await Assert.That(reload.Calls).IsEqualTo(1)
            .Because("a flight opened from the browser is a flight the queue does not have.");
    }

    [Test]
    public async Task A_duplicate_warning_does_not_touch_the_queue()
    {
        // THE ONE THAT MATTERS MOST. The confirmation has opened NOTHING yet and
        // is asking about a row; rebuilding the queue underneath it is how a
        // person ends up answering a question about something they are no longer
        // looking at.
        var reload = new Reloads(Booted());
        var ui = new Presses(Command.FlyPicked);

        var final = new ConsoleLoop(
            ui, new NoEditor(), actions: new Answers(alreadyFlown: "GG-7 already flew this"),
            reload: reload.Load).Run(Browsing());

        await Assert.That(final.Mode).IsEqualTo(UiMode.ConfirmFlight)
            .Because("the fixture has to actually reach the question, or this asserts about "
                   + "the wrong ending.");
        await Assert.That(reload.Calls).IsEqualTo(0)
            .Because("nothing has been opened, so there is nothing new to read - and reading "
                   + "would move the ground under an open question.");
    }

    [Test]
    public async Task Nothing_selected_does_not_touch_the_queue()
    {
        var reload = new Reloads(Booted());
        var ui = new Presses(Command.FlyPicked);

        new ConsoleLoop(ui, new NoEditor(), actions: new Answers(), reload: reload.Load)
            .Run(Booted() with { BrowseVisible = true });

        await Assert.That(reload.Calls).IsEqualTo(0)
            .Because("no row was picked, so no flight was opened, so nothing changed to "
                   + "re-read.");
    }

    [Test]
    public async Task A_write_that_came_back_a_refusal_is_still_re_read()
    {
        // THE FLAG WAS DECIDED BY MATCHING PROSE, AND THE PROSE MATCHED. A
        // refused flight comes back from VerbConsoleActions as
        //
        //     $"Nothing was opened - {refusal.Message}"
        //
        // and the old flag was, literally, "the sentence does not start with
        // 'Nothing was opened'". So every refusal skipped the re-read.
        //
        // WHICH IS WRONG ON THE ONE REFUSAL THAT MATTERS. Expected() catches
        // HttpRequestException, and a POST that reached the control plane and
        // then failed on the way back opens a flight and reports that nothing
        // was opened. A console that declines to re-read there stays wrong
        // about a flight it created, until somebody presses 'g'.
        //
        // A console cannot tell from a sentence whether anything changed. What
        // it knows is which branch ran.
        var reload = new Reloads(Booted() with { Queue = [Row("a", 1), Row("b", 2)] });
        var ui = new Presses(Command.FlyPicked);

        var final = new ConsoleLoop(
            ui, new NoEditor(), actions: new Refuses(), reload: reload.Load).Run(Browsing());

        await Assert.That(reload.Calls).IsEqualTo(1)
            .Because("FlyTicket ran, so the branch that opens flights is the branch that ran. "
                   + "What must NOT decide this is the wording of the answer.");
        await Assert.That(final.Queue.Count).IsEqualTo(2)
            .Because("the re-read landed on the model, not merely on a counter.");
    }

    /// <summary>
    /// Answers a flight with the refusal <c>VerbConsoleActions</c> really
    /// composes, opening words and all.
    /// </summary>
    private sealed class Refuses : IConsoleActions
    {
        public string Decide(string flight, string obligation, bool approved, string? reason) =>
            "Nothing was decided - the control plane could not be reached.";

        public string Fly(string intent) =>
            "Nothing was opened - the control plane could not be reached.";

        public string FlyTicket(string provider, string id) =>
            "Nothing was opened - the control plane could not be reached.";

        public string? AlreadyFlown(string provider, string id) => null;

        public string AddCredential() => "Nothing was registered.";

        public string Invite() => "Nothing was issued.";
    }

    private sealed class Throws
    {
        internal AppState Load(AppState _) =>
            throw new HttpRequestException("the control plane did not answer");
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => "";
    }

    private sealed class SomethingTyped : IEditorSession
    {
        public string Edit(string initialText) => "look at the thing";
    }

    private sealed class Answers(string? alreadyFlown = null) : IConsoleActions
    {
        public string Decide(string flight, string obligation, bool approved, string? reason) =>
            "decided";

        public string Fly(string intent) => "opened";

        public string FlyTicket(string provider, string id) => "opened";

        public string? AlreadyFlown(string provider, string id) => alreadyFlown;

        public string AddCredential() => "registered";

        public string Invite() => "invited";
    }
}
