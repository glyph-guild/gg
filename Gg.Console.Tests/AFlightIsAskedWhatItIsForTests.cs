using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Opening a flight asks what it is for, when the tenant has declared kinds.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>gg fly --work-kind</c> has worked from a terminal and been unreachable
/// from the console.</b> A tenant that declared work kinds could use them by
/// typing and not by pressing, on either door that opens a flight — so the
/// console was the one place the feature did not exist.
/// </para>
/// <para>
/// <b>Asked, and never remembered.</b> <c>ComposeChoiceTests</c> makes the
/// argument this inherits: a field holding the answer is one a later flight can
/// inherit, so the QUESTION is state and the ANSWER is a command that is gone
/// the moment it is handled. What is held here is a cursor — which row the
/// question is sitting on — exactly as the browse pane holds one.
/// </para>
/// <para>
/// <b>And not asked at all when there is nothing to choose.</b> A tenant with no
/// declared kinds has one possible answer, and a modal with one answer is
/// friction wearing a question's clothes. Flying is unchanged for them.
/// </para>
/// <para>
/// <b>The answer has to reach the SHELL.</b> The same trap one modal over: a
/// command the session handles never ends the session, so a modal that recorded
/// a choice and returned to Normal would leave an arm no key reaches. Opening a
/// flight happens between sessions with the terminal free, or it does not
/// happen.
/// </para>
/// </remarks>
public class AFlightIsAskedWhatItIsForTests
{
    private static AppState Press(AppState state, KeyStroke key) =>
        Keymap.Resolve(key, KeymapContext.For(state)) is { } command
            ? Reducer.Reduce(state, command)
            : state;

    private static EnvelopeTopology Declaring(params string[] kinds) => new()
    {
        Names =
        [
            new TopologyName
            {
                Name = "root",
                Role = Roles.Root,
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
            .. kinds.Select(kind => new TopologyName
            {
                Name = kind,
                Role = Roles.WorkKind,
                Parent = "root",
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            }),
        ],
    };

    /// <summary>A console that knows the tenant's kinds and is on no tab in particular.</summary>
    private static AppState WithKinds(params string[] kinds) => new()
    {
        Estate = new EstateOnThisMachine { Uncommitted = [], Names = Declaring(kinds) },
    };

    private static AppState Browsing(params string[] kinds) => new()
    {
        ActiveTab = TabId.Browse,
        BrowseVisible = true,
        Browse = new BrowseListing
        {
            ProviderKey = "jdx",
            Items = [new BrowseRow { Id = "18515", Title = "a thing to do", State = "Active" }],
        },
        Estate = kinds.Length == 0
            ? null
            : new EstateOnThisMachine { Uncommitted = [], Names = Declaring(kinds) },
    };

    [Test]
    public async Task Flying_a_picked_item_asks_what_it_is_for()
    {
        // THROUGH THE LOOP, BECAUSE `f' IS THE SHELL'S. A session-handled
        // command never ends the session, and opening a flight can only happen
        // between sessions - so the branch that asks lives where the confirm
        // modal's does, and a reducer test would be testing nothing.
        var actions = new ConsoleDoubles.Records(alreadyFlown: null);
        var asked = ConsoleLoop.FlewPicked(Browsing("hal-score", "research"), actions);

        await Assert.That(actions.Flown).IsEmpty()
            .Because("the whole point is that it has not happened yet.");

        await Assert.That(asked.Mode).IsEqualTo(UiMode.WorkKindChoice)
            .Because("a tenant that declared kinds has a choice to make, and the console was "
                   + "the one place it could not be made.");

        await Assert.That(asked.AskingKindFor).IsEqualTo(ComposingFor.WorkItem)
            .Because("the question has to remember which door asked it, or answering cannot "
                   + "resume what it interrupted.");
    }

    [Test]
    public async Task A_tenant_with_no_kinds_is_not_asked_at_all()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: null);
        var flown = ConsoleLoop.FlewPicked(Browsing(), actions);

        await Assert.That(flown.Mode).IsNotEqualTo(UiMode.WorkKindChoice)
            .Because("one possible answer is not a question, and a modal with one answer is "
                   + "friction wearing a question's clothes.");

        await Assert.That(actions.Flown).Count().IsEqualTo(1)
            .Because("and flying is unchanged for them, which is most tenants.");
    }

    [Test]
    public async Task The_question_offers_no_kind_first_and_then_the_declared_ones()
    {
        var asked = ConsoleLoop.FlewPicked(
            Browsing("hal-score", "research"), new ConsoleDoubles.Records(alreadyFlown: null));

        var said = PaneText.Modal(asked);

        await Assert.That(said).Contains("hal-score");
        await Assert.That(said).Contains("research");

        await Assert.That(asked.KindSelected).IsEqualTo(0)
            .Because("the cursor starts on `no kind', because that is what every flight "
                   + "before kinds existed was and it must stay the thing you get by "
                   + "pressing enter without reading.");
    }

    [Test]
    public async Task The_cursor_moves_and_the_answer_is_a_shell_command()
    {
        var asked = ConsoleLoop.FlewPicked(
            Browsing("hal-score", "research"), new ConsoleDoubles.Records(alreadyFlown: null));

        var moved = Press(asked, KeyStroke.Char('j'));

        await Assert.That(moved.KindSelected).IsEqualTo(1);

        var answer = Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(moved));

        await Assert.That(answer).IsNotNull();

        await Assert.That(ShellCommands.Handled.Contains(answer!.Value)).IsTrue()
            .Because("a command the session handles never ends the session, so a modal that "
                   + "recorded a choice and returned to Normal would leave an arm no key "
                   + "reaches - which is the defect the compose modal was found to have.");
    }

    [Test]
    public async Task Escaping_asks_for_no_flight_at_all()
    {
        var asked = ConsoleLoop.FlewPicked(
            Browsing("hal-score"), new ConsoleDoubles.Records(alreadyFlown: null));

        var escaped = Press(asked, KeyStroke.Esc);

        await Assert.That(escaped.Mode).IsEqualTo(UiMode.Normal);

        await Assert.That(escaped.AskingKindFor).IsEqualTo(ComposingFor.Nothing)
            .Because("escaping is `I did not mean to open this' and has to leave nothing "
                   + "behind - where choosing the first row is a decision to fly with no "
                   + "kind, which is a different act and opens a flight.");
    }

    [Test]
    public async Task A_new_flight_is_asked_before_it_is_composed()
    {
        // THE ORDER IS THE POINT. Asking after somebody has written a paragraph
        // is asking them to hold a question while they think about something
        // else - and the compose modal is already a question, so two in a row
        // have to come in the order a person can answer them.
        var asked = Press(WithKinds("hal-score"), KeyStroke.Char('n'));

        await Assert.That(asked.Mode).IsEqualTo(UiMode.WorkKindChoice);
        await Assert.That(asked.AskingKindFor).IsEqualTo(ComposingFor.NewFlight);

        // THROUGH THE LOOP AGAIN, because the answer is a shell command: a
        // reducer press would watch nothing happen and prove nothing.
        var composing = ConsoleLoop.ComposingNext(asked);

        await Assert.That(composing.Mode).IsEqualTo(UiMode.ComposeChoice)
            .Because("answering what it is FOR leads into how it gets written, which is the "
                   + "question that was already there.");
    }

    [Test]
    public async Task Flying_by_hand_is_asked_too()
    {
        var asked = Press(WithKinds("hal-score"), KeyStroke.Char('y'));

        await Assert.That(asked.Mode).IsEqualTo(UiMode.WorkKindChoice);
        await Assert.That(asked.AskingKindFor).IsEqualTo(ComposingFor.HandFlight)
            .Because("three doors open a flight and a question asked on two of them is a "
                   + "setting that works depending on how you started.");
    }

    [Test]
    public async Task And_neither_is_asked_when_the_tenant_declared_none()
    {
        await Assert.That(Press(new AppState(), KeyStroke.Char('n')).Mode)
            .IsEqualTo(UiMode.ComposeChoice)
            .Because("one possible answer is not a question, and flying is unchanged for a "
                   + "tenant that declared no kinds.");
    }

    [Test]
    public async Task Nothing_remembers_which_kind_was_picked()
    {
        var held = typeof(AppState).GetProperties()
            .Select(p => p.Name)
            .Where(name => name.Contains("ChosenKind", StringComparison.Ordinal)
                        || name.Contains("ChosenWorkKind", StringComparison.Ordinal)
                        || name.Contains("WorkKindChosen", StringComparison.Ordinal))
            .ToList();

        await Assert.That(held).IsEmpty()
            .Because("a field holding the kind is one a later flight can inherit, which is "
                   + "the argument the compose modal already makes - and the owner asked for "
                   + "each flight to be asked. Found: " + string.Join(", ", held));
    }
}
