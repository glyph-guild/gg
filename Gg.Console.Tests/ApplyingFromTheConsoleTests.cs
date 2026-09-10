using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Applying the working copy: the governed act, and the only one on this pane
/// that asks first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a key beside pull, and the difference is the whole design.</b> Pull
/// writes files and mints nothing; apply submits one amendment flight per
/// changed document, each with a gate, a version and an attribution. ADR-0016 §
/// 1 is that surfaces multiply and the pen does not — this is the pen being
/// reached from a third surface, so it looks like every other apply from the
/// control plane's side and takes a person's confirmation from this one.
/// </para>
/// <para>
/// <b>It asks WHETHER, which is <c>ConfirmGround</c>'s argument one act
/// over.</b> `x` used to hand the terminal away on a single keypress and the
/// only way out of a mistyped one was to write nothing and read the refusal.
/// Apply is worse than that: what it does is irreversible in the sense that
/// matters — a flight takes a number, is attributed, and is a record somebody
/// has to explain.
/// </para>
/// <para>
/// <b>And the question names the changeset, in the order it will land.</b>
/// Tightenings first, so no intermediate state is looser than either endpoint;
/// a person confirming an apply is confirming a sequence, and one that showed a
/// count alone would be asking them to agree to something they cannot see.
/// </para>
/// </remarks>
public class ApplyingFromTheConsoleTests
{
    /// <summary>
    /// A state with the question open over a changeset.
    /// </summary>
    /// <remarks>
    /// The MODE is part of the fixture, because <c>PaneText.Modal</c> dispatches
    /// on it - a state carrying the changeset and not the question renders the
    /// empty string, which would have made three assertions here pass against
    /// nothing had they looked for an absence instead of a presence.
    /// </remarks>
    private static AppState WithChanges(params DocumentChange[] changes) =>
        Carrying(UiMode.ConfirmApply, changes);

    /// <summary>
    /// A state carrying a changeset, in the mode named.
    /// </summary>
    /// <remarks>
    /// <b>The mode is part of the fixture twice over.</b>
    /// <c>PaneText.Modal</c> dispatches on it, so a state carrying the
    /// changeset and not the question renders the empty string; and
    /// <c>Reducer.Modal</c> TOGGLES, so asking the question from inside it
    /// closes it - which is right, and means the reducer's test has to start in
    /// Normal like a person does.
    /// </remarks>
    private static AppState Carrying(UiMode mode, params DocumentChange[] changes) => new()
    {
        Mode = mode,
        ActiveTab = TabId.Envelope,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/estate",
            IsRepository = true,
            Names = new Gg.Contracts.EnvelopeTopology { Names = [] },
            Working = new EstateDiff
            {
                Changes = changes,
                Retiring = [],
                Unreadable = [],
            },
        },
    };

    private static DocumentChange Change(string name, string direction, string? field = null) =>
        new()
        {
            Name = name,
            Path = $"airspace/narrowings/{name}.yaml",
            Direction = direction,
            Field = field,
        };

    [Test]
    public async Task The_key_asks_rather_than_applying()
    {
        var command = Keymap.Resolve(
            KeyStroke.Char('s'), new KeymapContext(UiMode.Normal, TabId.Envelope));

        await Assert.That(command).IsEqualTo(Command.AskToApplyEstate)
            .Because("one keypress that opens a flight per changed document, each taking a "
                   + "number and an attribution, is a mistype somebody has to explain.");

        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.AskToApplyEstate)
            .Because("asking holds no I/O at all, which is why it can happen inside a "
                   + "session while the thing it asks about happens outside one.");
    }

    [Test]
    public async Task Asking_opens_the_question_and_nothing_else()
    {
        var asked = Reducer.Reduce(
            Carrying(UiMode.Normal, Change("pci", Changeset.Widening)),
            Command.AskToApplyEstate);

        await Assert.That(asked.Mode).IsEqualTo(UiMode.ConfirmApply);
    }

    [Test]
    public async Task The_question_is_answered_yes_and_escaped_out_of()
    {
        var inside = new KeymapContext(UiMode.ConfirmApply, TabId.Envelope);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('y'), inside))
            .IsEqualTo(Command.ApplyEstate);

        await Assert.That(Keymap.EscapeHatch(inside)).IsEqualTo(KeyStroke.Esc)
            .Because("a modal owns the keyboard, and exactly one way out is what stops the "
                   + "terminal being locked up.");
    }

    [Test]
    public async Task Applying_is_the_shell_s_work()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.ApplyEstate)
            .Because("it opens flights, and a UI session may not make a network call at "
                   + "all - let alone one that mints a version.");

        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.ApplyEstate);
    }

    [Test]
    public async Task The_question_lists_them_in_the_order_the_diff_gave()
    {
        // PRESERVED, NOT SORTED, and the distinction is the claim. The diff
        // verb already returns its changes in the safe order - tightenings
        // before widenings, ADR-0016 § 7 - for the stated reason that "the
        // order a person reads is the order that will happen", and slice one's
        // own test holds it to that. Sorting again here would be a second
        // opinion about a sequence; what this asserts is that the modal does
        // not scramble the one it was handed, because a question describing a
        // different order from the pane behind it is a review of something that
        // will not occur.
        var text = PaneText.Modal(WithChanges(
            Change("root", Changeset.Tightening),
            Change("pci", Changeset.Widening, "obligations")));

        var first = text.IndexOf("root", StringComparison.Ordinal);
        var second = text.IndexOf("pci", StringComparison.Ordinal);

        await Assert.That(first).IsGreaterThanOrEqualTo(0);
        await Assert.That(second).IsGreaterThan(first)
            .Because("the changes arrive in the order apply will take them, and a modal that "
                   + "reordered them would describe a sequence that will not happen.");
    }

    [Test]
    public async Task The_question_says_which_changes_will_wait_for_a_person()
    {
        var text = PaneText.Modal(WithChanges(
            Change("pci", Changeset.Widening, "obligations")));

        await Assert.That(text).Contains("gate", StringComparison.OrdinalIgnoreCase)
            .Because("a widening does not land - it opens a flight and waits - and somebody "
                   + "who expected it to land will go looking for a version that was never "
                   + "minted.");
    }

    [Test]
    public async Task Nothing_changed_is_said_rather_than_asked_about()
    {
        // THE POSITIVE CONTROL FOR THE QUESTION ITSELF. A modal that asked
        // "apply 0 documents?" would be a keypress that does nothing, twice.
        var text = PaneText.Modal(WithChanges());

        await Assert.That(text).Contains("nothing", StringComparison.OrdinalIgnoreCase)
            .Because("the working copy matching the estate is the ordinary state, and it is "
                   + "an answer rather than a question.");
    }

    [Test]
    public async Task A_divert_is_reported_with_its_flight_and_its_approver()
    {
        var said = ConsoleApply.Applied(() => new VerbResult.AirspaceApplied(new EstateApplied
        {
            Applied =
            [
                new AppliedDocument
                {
                    Name = "root", Path = "airspace/root.yaml",
                    Version = "root@v8", Changed = true,
                },
                new AppliedDocument
                {
                    Name = "pci", Path = "airspace/narrowings/pci.yaml",
                    Version = "pci@v2", Changed = false,
                    Widens = "obligations", Flight = "GG-58", Awaiting = "an-auditor",
                },
            ],
            Retiring = [],
        }));

        await Assert.That(said).Contains("root@v8", StringComparison.Ordinal)
            .Because("a minted version is the thing a person quotes afterwards.");
        await Assert.That(said).Contains("GG-58", StringComparison.Ordinal);
        await Assert.That(said).Contains("an-auditor", StringComparison.Ordinal)
            .Because("a gate with nobody named is a gate a person cannot go and ask about.");
    }

    [Test]
    public async Task An_unreadable_file_stops_the_apply_and_is_rendered_as_that()
    {
        var said = ConsoleApply.Applied(
            () => throw new EnvelopeRefusedException(
                "The working copy holds files that sit where a document goes and do not read "
              + "as one. Nothing was applied, because applying the rest would land part of a "
              + "changeset somebody meant as a whole:\n"
              + "  airspace/narrowings/pci.yaml: This does not read as a narrowing."));

        await Assert.That(said).Contains("pci.yaml", StringComparison.Ordinal)
            .Because("one bad file stops every document, so a person told only that the "
                   + "apply failed has to go and find which.");
        await Assert.That(said).DoesNotContain("Exception", StringComparison.Ordinal);
    }
}
