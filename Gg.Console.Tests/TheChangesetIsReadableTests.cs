using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The changeset is readable from the console, in the order apply will run it.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT WAS ONLY READABLE FROM <c>gg airspace diff</c>.</b> The console has
/// been fetching it all along — <c>ConsoleEstate</c> calls the diff verb and
/// holds an <c>EstateDiff</c> — and nothing rendered it except the apply
/// confirmation, which a person sees only once they have already decided to
/// apply. So the question "what would this change" had no answer on the
/// surface somebody is sitting at.
/// </para>
/// <para>
/// <b>THE SECOND VIEW OF ONE MODAL, not a second key.</b> `d` is `decide` in
/// Normal mode and a tab-scoped one would never fire, since
/// <c>Keymap.Resolve</c> answers the first match. Inside a modal the letters
/// are free — <c>HostedBar</c>'s own argument — so `v` opens and `d` and `e`
/// switch.
/// </para>
/// <para>
/// <b>The order is the diff's own and is never re-sorted.</b>
/// <c>Changeset.InSafeOrder</c> already ran: tightenings first, so no
/// intermediate state is looser than either endpoint. A view listing changes
/// in one order while apply ran them in another would be a review of something
/// that never happens.
/// </para>
/// </remarks>
public class TheChangesetIsReadableTests
{
    private static AppState With(EstateDiff? working, string? diagnosis = null) => new()
    {
        Mode = UiMode.ReadingChangeset,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",
            Uncommitted = [],
            Working = working,
            Diagnosis = diagnosis,
            Tree = new WorkingCopy
            {
                Present = true,
                Documents = [new("root", "root", "airspace/root.yaml", "v6")],
                Unreadable = [],
            },
        },
    };

    private static EstateDiff Diffed() => new()
    {
        Changes =
        [
            new DocumentChange
            {
                Name = "implement",
                Path = "airspace/work-kinds/implement.yaml",
                Direction = Changeset.Tightening,
            },
            new DocumentChange
            {
                Name = "pci",
                Path = "airspace/narrowings/pci.yaml",
                Direction = Changeset.Widening,
                Field = "obligations",
                Because = "adding constrains anyone, and the beneficiary owns removal",
            },
        ],
        Retiring = ["team-pay"],
        Unreadable = [],
    };

    [Test]
    public async Task Every_changed_document_is_named_with_which_way_it_moves()
    {
        var said = string.Join('\n', PaneText.ChangesetLines(With(Diffed()), 0));

        await Assert.That(said).Contains("implement", StringComparison.Ordinal);
        await Assert.That(said).Contains(Changeset.Tightening, StringComparison.Ordinal)
            .Because("which way it moves is what decides whether it lands or waits, and it "
                   + "is the whole reason to read this before applying. Said:\n" + said);

        await Assert.That(said).Contains("pci", StringComparison.Ordinal);
        await Assert.That(said).Contains(Changeset.Widening, StringComparison.Ordinal);
    }

    [Test]
    public async Task A_widening_says_which_field_and_why()
    {
        var said = string.Join('\n', PaneText.ChangesetLines(With(Diffed()), 0));

        await Assert.That(said).Contains("obligations", StringComparison.Ordinal)
            .Because("the field that widened is the part somebody can act on.");

        await Assert.That(said).Contains("beneficiary owns removal", StringComparison.Ordinal)
            .Because("and `Because` is the door's own explanation of why it could not be "
                   + "shown to tighten - carried on DocumentChange since it was written and "
                   + "rendered by nothing until now. Said:\n" + said);
    }

    [Test]
    public async Task The_order_is_the_order_apply_will_run()
    {
        var said = PaneText.ChangesetLines(With(Diffed()), 0).ToList();

        var tightening = said.FindIndex(l => l.Contains("implement", StringComparison.Ordinal));
        var widening = said.FindIndex(l => l.Contains("pci", StringComparison.Ordinal));

        await Assert.That(tightening).IsLessThan(widening)
            .Because("tightenings before widenings, so no intermediate state is looser "
                   + "than either endpoint - and the diff already sorted it, so this must "
                   + "not sort it again.");
    }

    [Test]
    public async Task A_retiring_name_is_reported_as_the_intent_it_is()
    {
        var said = string.Join('\n', PaneText.ChangesetLines(With(Diffed()), 0));

        await Assert.That(said).Contains("team-pay", StringComparison.Ordinal);
        await Assert.That(said).Contains("retir", StringComparison.OrdinalIgnoreCase)
            .Because("a name the estate holds that the tree does not. Changeset.Retirement "
                   + "is declared, ranked and unreachable - no client method calls the "
                   + "retirement endpoint - so this is an intent with nothing behind it and "
                   + "says so rather than implying apply will perform it.");
    }

    [Test]
    public async Task Nothing_to_apply_is_not_the_same_as_could_not_ask()
    {
        var matching = string.Join('\n', PaneText.ChangesetLines(
            With(new EstateDiff { Changes = [], Retiring = [], Unreadable = [] }), 0));

        await Assert.That(matching).Contains("matches", StringComparison.OrdinalIgnoreCase)
            .Because("a clean working copy is an answer. Said:\n" + matching);

        var refused = string.Join('\n', PaneText.ChangesetLines(
            With(null, diagnosis: "Not signed in. Run gg login first."), 0));

        await Assert.That(refused).Contains("Not signed in", StringComparison.Ordinal)
            .Because("and one that could not be compared is a different answer with a "
                   + "different fix. Said:\n" + refused);

        await Assert.That(refused).Contains("git", StringComparison.OrdinalIgnoreCase)
            .Because("and it says what the rows ARE showing meanwhile, because they are "
                   + "showing something: which documents git says you have edited. Said:\n"
                   + refused);
    }

    [Test]
    public async Task Not_asked_yet_is_a_third_answer()
    {
        var said = string.Join('\n', PaneText.ChangesetLines(With(null), 0));

        await Assert.That(said).DoesNotContain("Not signed in", StringComparison.Ordinal);
        await Assert.That(said).Contains("not", StringComparison.OrdinalIgnoreCase)
            .Because("nothing pulled, nothing changed and nothing asked are three facts "
                   + "and only one of them is a thing to go and fix - which is the "
                   + "distinction PaneText.Estate already made and this must keep.");
    }

    [Test]
    public async Task An_unreadable_file_says_it_refuses_the_whole_apply()
    {
        var said = string.Join('\n', PaneText.ChangesetLines(With(new EstateDiff
        {
            Changes = [],
            Retiring = [],
            Unreadable = ["airspace/narrowings/broken.yaml"],
        }), 0));

        await Assert.That(said).Contains("broken.yaml", StringComparison.Ordinal);
        await Assert.That(said).Contains("every apply", StringComparison.OrdinalIgnoreCase)
            .Because("apply refuses the whole changeset over one of these rather than "
                   + "landing the rest, because the rest is part of something somebody "
                   + "meant as a whole.");
    }

    [Test]
    public async Task The_two_views_are_reachable_from_each_other()
    {
        // WITHOUT LEAVING AND COMING BACK, which is HostedBar's reason for the
        // same pair of keys: comparing what governs against what you are about
        // to change is why both are here.
        var reading = new AppState { Mode = UiMode.ReadingEnvelope, ActiveTab = TabId.Envelope };

        await Assert.That(Keymap.Resolve(
                KeyStroke.Char('d'), KeymapContext.For(reading)))
            .IsEqualTo(Command.ReadChangeset)
            .Because("inside a modal the letters are free, which is why the changeset costs "
                   + "no Normal-mode letter at all.");

        var changeset = reading with { Mode = UiMode.ReadingChangeset };

        await Assert.That(Keymap.Resolve(
                KeyStroke.Char('e'), KeymapContext.For(changeset)))
            .IsEqualTo(Command.ReadEnvelope)
            .Because("and back again.");

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, KeymapContext.For(changeset)))
            .IsEqualTo(Command.CloseModal)
            .Because("one way out, from either view.");
    }
}
