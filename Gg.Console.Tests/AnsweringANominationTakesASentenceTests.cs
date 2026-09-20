using System.Text.RegularExpressions;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The board's answer key: opening or declining the nomination under the
/// cursor, and the sentence that has to be written first.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.6-04, and rule 7 is what it finishes.</b> An executor nominates and
/// the board decides. Every other half of that sentence is built - a watch
/// sweeps, a nomination stands, the tab shows it - and a board with no way to
/// answer is a list of work waiting on a person who has to leave for a shell
/// to act on it.
/// </para>
/// <para>
/// <b>It arrives with a confirmation the other row keys do not have, and that
/// confirmation is a sentence.</b> Every other key on a row in this console is
/// reversible: a tab is closed again, a modal is escaped, a cursor moves back.
/// Opening a nomination starts a flight - a record somebody has to explain and
/// a number that is now taken, which is <c>ConfirmFlight</c>'s reason. So this
/// one asks for something a second keypress cannot produce.
/// </para>
/// <para>
/// <b>The sentence is not ceremony this console invented.</b> The door already
/// refuses a decision with no <c>because</c>, for both answers - "it is the
/// only thing that survives to tell a later reader why a person opened work
/// nobody had asked for, or declined work somebody had". A <c>y/n</c> box in
/// front of that would be two confirmations, and the one that is already
/// required is the one with something in it afterwards.
/// </para>
/// <para>
/// <b>And the answer is posted through the verb, never recorded here.</b> What
/// the row BECOMES is an admission pass that may refuse; a pane that marked it
/// opened when a key was pressed would advance on a claim rather than on what
/// happened - Article IX, and the reason <c>GateDecision</c>'s reducer arms
/// return the state untouched.
/// </para>
/// </remarks>
public class AnsweringANominationTakesASentenceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Standing = new("7f1b9e42-0000-4000-8000-000000000001");

    private static NominationSummary ANomination(
        Guid? id = null,
        string subject = "work-item:https://tracker.example/acme/4242",
        string mode = "gated",
        string? ending = null,
        int minutesOld = 30) => new()
    {
        NominationId = id ?? Standing,
        Nominator = "watch:nightly-triage",
        Subject = subject,
        Version = "7",
        WorkKind = "review",
        Mode = mode,
        State = ending is null ? "standing" : "ended",
        Ending = ending,
        Because = ending is null ? null : "somebody had already done it",
        MadeAt = Noon.AddMinutes(-minutesOld),
    };

    private static WatchStanding AWatch() => new()
    {
        Name = "nightly-triage",
        Version = "nightly-triage@v1",
        Executor = WatchExecutors.Instructions,
        LastHeardAt = Noon.AddMinutes(-10),
        Outcome = WatchOutcomes.Swept,
        Nominated = 2,
        Opened = 3,
        Window = "24h",
        Budgeted = 5,
    };

    /// <summary>The board, with the cursor wherever the caller puts it.</summary>
    private static AppState Board(
        int selected = 0,
        IReadOnlyList<NominationSummary>? nominations = null) => new()
    {
        ActiveTab = TabId.Board,
        BoardSelected = selected,
        Board = new BoardPage
        {
            Nominations = nominations ?? [ANomination()],
            IncludedEnded = true,
        },
        Watches = new WatchStandingList { Standings = [AWatch()] },
    };

    [Test]
    public async Task Enter_on_a_standing_nomination_opens_the_question()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(Board())))
            .IsEqualTo(Command.ShowBoardRow)
            .Because("enter opens what the cursor is on, on every tab that lists something - "
                   + "and on this one what it opens now carries the reason as well as the "
                   + "question, which is what a person answers with.");
    }

    [Test]
    public async Task The_question_offers_both_answers_and_exactly_one_way_out()
    {
        var asking = Board() with { Mode = UiMode.BoardDetail };
        var context = KeymapContext.For(asking);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), context))
            .IsEqualTo(Command.OpenNomination);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), context))
            .IsEqualTo(Command.DeclineNomination);

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, context)).IsEqualTo(Command.CloseModal)
            .Because("one way out of every modal, the same key everywhere, which is what makes "
                   + "it findable without being learned.");

        // BOTH ANSWERS TOGETHER, which is a modal's whole reason over a menu
        // item: declining has to be as reachable as opening, or the console has
        // an opinion about which one a person came here to give.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('n'), context)).IsNull()
            .Because("the modal has the keyboard while it is open, and `n` opens a flight in "
                   + "Normal mode - one reaching through this question would open work nobody "
                   + "was asked about.");
    }

    [Test]
    public async Task The_question_names_the_row_and_says_what_each_answer_costs()
    {
        var text = PaneText.Modal(Board() with { Mode = UiMode.BoardDetail });

        await Assert.That(text).Contains("4242")
            .Because("two nominations from one watch differ only in their subject, so a "
                   + "question that did not name it is one nobody can answer safely.");
        await Assert.That(text).Contains("review")
            .Because("what the flight would be FOR is the other half of what is being agreed "
                   + "to - a review and an implement are different amounts of somebody's "
                   + "afternoon.");
        await Assert.That(text.Contains("editor", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the editor opens next and that is where the answer really lands. A "
                   + "question that did not say so reads as though the key finishes it.");
    }

    [Test]
    public async Task Both_answers_are_the_shells_because_each_of_them_writes()
    {
        foreach (var answer in (Command[])[Command.OpenNomination, Command.DeclineNomination])
        {
            await Assert.That(ShellCommands.Handled).Contains(answer)
                .Because($"{answer} posts to the control plane and hands the terminal to "
                       + "$EDITOR on the way, and both of those happen between sessions with "
                       + "the terminal provably free. A pure reduction here would answer "
                       + "nothing at all - the defect ComposeInEditor already recorded.");
        }
    }

    [Test]
    public async Task The_reducer_settles_nothing_because_the_control_plane_does()
    {
        var asking = Board() with { Mode = UiMode.BoardDetail };

        foreach (var answer in (Command[])[Command.OpenNomination, Command.DeclineNomination])
        {
            await Assert.That(Reducer.Reduce(asking, answer)).IsEqualTo(asking)
                .Because("answering posts; it does not decide. Opening starts an admission "
                       + "pass that may refuse, so a reducer that ended the row here would be "
                       + "the console deciding - Article IX in its softest clothing, which is "
                       + "the dangerous kind, because the demo works.");
        }
    }

    [Test]
    public async Task Answering_sends_the_row_the_outcome_and_the_reason()
    {
        var records = new ConsoleDoubles.Records();

        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.OpenNomination),
                new ConsoleDoubles.Writes("the tracker has had this open for three weeks"),
                actions: records)
            .Run(Board() with { Mode = UiMode.BoardDetail });

        await Assert.That(records.Answered).IsEquivalentTo(new[]
        {
            (Standing.ToString(), true, "the tracker has had this open for three weeks"),
        })
            .Because("the row, what was answered, and why - and the id rather than the "
                   + "subject, because two nominations can name one work item and only one of "
                   + "them is under the cursor.");

        await Assert.That(final.LastNomination).IsNotNull()
            .Because("what happened when the key was pressed is the only fact this console is "
                   + "entitled to, and a person who saw nothing does not know whether it "
                   + "went.");
    }

    [Test]
    public async Task Declining_sends_the_other_outcome_and_the_same_reason()
    {
        var records = new ConsoleDoubles.Records();

        _ = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.DeclineNomination),
                new ConsoleDoubles.Writes("this repository is being retired next month"),
                actions: records)
            .Run(Board() with { Mode = UiMode.BoardDetail });

        await Assert.That(records.Answered).IsEquivalentTo(new[]
        {
            (Standing.ToString(), false, "this repository is being retired next month"),
        })
            .Because("declining is an answer rather than a dismissal, and it is the one that "
                   + "most needs its reason kept: the next sweep will find the same item and "
                   + "somebody will want to know why it was turned down.");
    }

    [Test]
    public async Task An_empty_buffer_answers_nothing_and_says_so()
    {
        // THE CONFIRMATION, AND IT IS THE WORK. This is the only key on a row in
        // this console that cannot be given by accident, because the thing it
        // asks for is a sentence - and a person who changes their mind between
        // pressing `o` and saving has not opened a flight.
        foreach (var answer in (Command[])[Command.OpenNomination, Command.DeclineNomination])
        {
            var records = new ConsoleDoubles.Records();

            var final = new ConsoleLoop(
                    new ConsoleDoubles.TypesKeys(answer),
                    new ConsoleDoubles.Writes("   \n  "),
                    actions: records)
                .Run(Board() with { Mode = UiMode.BoardDetail });

            await Assert.That(records.Answered).IsEmpty()
                .Because("whitespace is not a reason, and the door refuses one anyway - "
                       + "refusing here as well means a person who saved an empty buffer has "
                       + "not answered by accident.");

            await Assert.That(
                    final.LastNomination!.Contains(
                        "nothing was sent", StringComparison.OrdinalIgnoreCase)).IsTrue()
                .Because("falling silent is indistinguishable from a console that is broken, "
                       + "and this is the path a person reaches by changing their mind.");
        }
    }

    [Test]
    public async Task Answering_re_reads_the_board_because_a_decision_changes_what_is_waiting()
    {
        var reloads = new ConsoleDoubles.Reloads(new AppState());

        _ = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.OpenNomination),
                new ConsoleDoubles.Writes("it is the third time this week"),
                actions: new ConsoleDoubles.Records(),
                reload: reloads.Load)
            .Run(Board() with { Mode = UiMode.BoardDetail });

        await Assert.That(reloads.Calls).IsEqualTo(1)
            .Because("rule 4: answering changes what is on this board, and a row still "
                   + "standing after somebody answered it is the staleness this console keeps "
                   + "being caught by.");
    }

    [Test]
    public async Task Exactly_one_thing_writes_a_nomination_decision()
    {
        // THE GATE'S GUARD, APPLIED TO THE OTHER DECISION. Two paths to one
        // state transition is how a console's view and the control plane's
        // record drift apart, and nothing would say which was right.
        var writers = Under("Gg.Client")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bDecideNominationAsync\s*\("))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(writers).IsEquivalentTo(new[]
        {
            // The verb, calling it.
            "ControlPlaneClient.cs",

            // Declared and implemented: the one place a nomination is answered.
            "FlightCommands.cs",
        })
            .Because("a third file posting a decision is a second path to one transition. "
                   + "Found: " + string.Join(", ", writers));

        var console = Under("Gg.Console")
            .Where(f => File.ReadAllText(f).Contains("DecideNominationAsync", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(console).IsNotEmpty()
            .Because("the other half: a structural test that only counted writers would pass "
                   + "if the console answered nothing at all.");
    }

    private static IEnumerable<string> Under(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return Directory.EnumerateFiles(
            Path.Combine(dir!.FullName, project), "*.cs", SearchOption.AllDirectories);
    }
}
