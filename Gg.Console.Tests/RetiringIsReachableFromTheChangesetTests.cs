using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The retirements the changeset reports can be performed from where it
/// reports them.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT REPORTED AN INTENT AND NOTHING COULD ACT ON IT.</b> The changeset
/// view says <i>"X is missing from the tree — retiring a name is its own gated
/// change, and nothing here performs it"</i>, which was true of the whole
/// program: the door was in the contract and no client method called it. So a
/// name, once declared, was permanent.
/// </para>
/// <para>
/// <b>Reached from the modal, because a deleted document HAS NO ROW.</b> The
/// obvious affordance would be a key on the tree row — but the names that can
/// be retired are exactly the ones whose files are gone, so there is no row to
/// put a cursor on. The changeset view is where they are listed, so it is
/// where the key goes.
/// </para>
/// <para>
/// <b>And NOT a Normal-mode letter.</b> `x` is <c>ForgetCredential</c> in
/// Normal mode, and the <c>TabId.Envelope</c> spread is declared EARLIER than
/// that global — so a tab-scoped `x` would win and silently take forget-
/// credential away on that tab. Inside a modal the letters are free, which is
/// the same reason the changeset and outcome views are reached with letters
/// rather than keys of their own.
/// </para>
/// <para>
/// <b>All of them at once, which is what the changeset describes.</b> No
/// cursor is invented for the modal's list: the report is "these names are
/// missing", and performing it is one act over that set.
/// </para>
/// </remarks>
public class RetiringIsReachableFromTheChangesetTests
{
    private static AppState Reading(IReadOnlyList<string> retiring) => new()
    {
        Mode = UiMode.ReadingChangeset,
        ActiveTab = TabId.Envelope,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",
            Uncommitted = [],
            Working = new EstateDiff
            {
                Changes = [],
                Retiring = retiring,
                Unreadable = [],
            },
            Tree = new WorkingCopy { Present = true, Documents = [], Unreadable = [] },
        },
    };

    [Test]
    public async Task A_letter_in_the_changeset_view_asks_to_retire_them()
    {
        var reading = Reading(["score-hall"]);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), KeymapContext.For(reading)))
            .IsEqualTo(Command.AskToRetire)
            .Because("the names that can be retired are listed here and nowhere else - a "
                   + "deleted document has no tree row to put a cursor on.");
    }

    [Test]
    public async Task It_does_not_take_a_letter_away_from_normal_mode()
    {
        // THE TRAP THIS AVOIDS. `x` is ForgetCredential in Normal mode, and
        // the Envelope tab's spread is declared EARLIER than that global - so
        // a tab-scoped `x` would win and take forget-credential away on that
        // tab, silently. Inside a modal nothing is shadowed.
        var normal = new AppState { ActiveTab = TabId.Envelope };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), KeymapContext.For(normal)))
            .IsEqualTo(Command.ForgetCredential)
            .Because("binding a free key on a tab silently kills the fall-through it had, "
                   + "which this repository has been bitten by before.");
    }

    [Test]
    public async Task The_question_names_them_and_says_it_always_opens_a_gate()
    {
        var asking = Reducer.Reduce(Reading(["score-hall", "old-pci"]), Command.AskToRetire);

        await Assert.That(asking.Mode).IsEqualTo(UiMode.ConfirmRetire);

        var said = PaneText.Modal(asking);

        await Assert.That(said).Contains("score-hall", StringComparison.Ordinal);
        await Assert.That(said).Contains("old-pci", StringComparison.Ordinal)
            .Because("every name it would retire, because this is the last screen before "
                   + "an act that needs an approver to reverse. Said: " + said);

        await Assert.That(said).Contains("gate", StringComparison.OrdinalIgnoreCase)
            .Because("retiring removes every constraint in a document at once, so it is a "
                   + "widening by construction and has no immediate form. Somebody who "
                   + "expected the name to be gone would go looking for something that "
                   + "has not happened. Said: " + said);

        await Assert.That(said).Contains("governs", StringComparison.OrdinalIgnoreCase)
            .Because("and until that gate opens the name still governs, which is the part "
                   + "that surprises people.");
    }

    [Test]
    public async Task Nothing_missing_is_an_answer_rather_than_a_question()
    {
        var asking = Reducer.Reduce(Reading([]), Command.AskToRetire);

        var said = PaneText.Modal(asking);

        await Assert.That(said).Contains("nothing", StringComparison.OrdinalIgnoreCase)
            .Because("the apply question's own shape: a modal asking about zero documents "
                   + "is a keypress that does nothing, twice. Said: " + said);
    }

    [Test]
    public async Task Answering_it_is_the_loops_work_and_not_a_sessions()
    {
        // A NETWORK WRITE, so it ends the session like every other one. A
        // session may read a local file and nothing else.
        await Assert.That(ShellCommands.Handled).Contains(Command.RetireNames)
            .Because("it makes a request per name, which is the loop's to do with the "
                   + "terminal released.");

        var asking = Reducer.Reduce(Reading(["score-hall"]), Command.AskToRetire);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('y'), KeymapContext.For(asking)))
            .IsEqualTo(Command.RetireNames);

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, KeymapContext.For(asking)))
            .IsEqualTo(Command.CloseModal)
            .Because("one way out, and it is the way out of an irreversible-ish act.");
    }
}
