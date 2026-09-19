using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The notifications in the corner: two keys that reach them from anywhere, and
/// a mode that holds the keyboard only when somebody asks it to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unfocused, they have no keyboard at all.</b> A notification arrives while
/// a person is doing something else; one that took the next key would eat
/// whatever they were halfway through. So from the main view there are exactly
/// two keys, offered only while something is showing: <c>&gt;</c> goes to it,
/// and <c>!</c> picks the stack up.
/// </para>
/// <para>
/// <b>Focused, they are a modal like any other</b>: their own keys, a button
/// for each, every other key swallowed, and esc as the one way out.
/// </para>
/// </remarks>
public class TheNotificationsInTheCornerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);

    private static Notification Opened(string id, string number) => new()
    {
        Kind = NotificationKind.FlightOpened,
        FlightId = id,
        FlightNumber = number,
        Name = $"work {number}",
    };

    private static AppState Showing(params Notification[] notifications) => new()
    {
        Flights = new FlightList
        {
            Flights = [AFlight("f-1", 1, 1), AFlight("f-2", 2, 2), AFlight("f-3", 3, 3)],
        },
        Notifications = notifications,
        NotificationAt = notifications.Length - 1,
    };

    [Test]
    public async Task The_two_keys_are_offered_only_while_something_is_showing()
    {
        var quiet = KeymapContext.For(new AppState());
        var showing = KeymapContext.For(Showing(Opened("f-2", "GG-2")));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('>'), quiet)).IsNull();
        await Assert.That(Keymap.Resolve(KeyStroke.Char('!'), quiet)).IsNull()
            .Because("a key that does nothing because nothing is showing is a dead key.");

        await Assert.That(Keymap.Resolve(KeyStroke.Char('>'), showing))
            .IsEqualTo(Command.GoToNotification);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('!'), showing))
            .IsEqualTo(Command.ShowNotifications);
    }

    [Test]
    public async Task Picking_them_up_is_a_mode_and_nothing_showing_opens_nothing()
    {
        var picked = Reducer.Reduce(Showing(Opened("f-2", "GG-2")), Command.ShowNotifications);
        var nothing = Reducer.Reduce(new AppState(), Command.ShowNotifications);

        await Assert.That(picked.Mode).IsEqualTo(UiMode.Notifications);
        await Assert.That(nothing.Mode).IsEqualTo(UiMode.Normal)
            .Because("a modal whose only content is the way out is a key that appears to "
                   + "work.");
    }

    [Test]
    public async Task Focused_they_answer_their_own_keys_and_esc_is_the_way_out()
    {
        var several = KeymapContext.For(
            Showing(Opened("f-1", "GG-1"), Opened("f-2", "GG-2")) with { Mode = UiMode.Notifications });

        await Assert.That(Keymap.Resolve(KeyStroke.RightKey, several)).IsEqualTo(Command.NextNotification);
        await Assert.That(Keymap.Resolve(KeyStroke.LeftKey, several)).IsEqualTo(Command.PreviousNotification);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), several)).IsEqualTo(Command.DismissNotification);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('>'), several)).IsEqualTo(Command.GoToNotification);
        await Assert.That(Keymap.Resolve(KeyStroke.Esc, several)).IsEqualTo(Command.CloseModal);

        // THE MAIN VIEW'S KEYS DO NOT REACH THROUGH. `u` is the runners tab
        // everywhere else; here it is nobody's.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('u'), several)).IsNull();
    }

    [Test]
    public async Task One_notification_has_nothing_to_page_to()
    {
        var one = KeymapContext.For(Showing(Opened("f-1", "GG-1")) with { Mode = UiMode.Notifications });

        await Assert.That(Keymap.Resolve(KeyStroke.RightKey, one)).IsNull();
        await Assert.That(Keymap.Buttons(one).Select(b => b.Label ?? "")).IsEquivalentTo(
            new[] { "Go to it", "Dismiss" });
    }

    [Test]
    public async Task Several_have_a_button_each_way()
    {
        var several = KeymapContext.For(
            Showing(Opened("f-1", "GG-1"), Opened("f-2", "GG-2")) with { Mode = UiMode.Notifications });

        await Assert.That(Keymap.Buttons(several).Select(b => b.Label ?? "")).IsEquivalentTo(
            new[] { "‹", "›", "Go to it", "Dismiss" });
    }

    [Test]
    public async Task Paging_goes_round()
    {
        var three = Showing(Opened("f-1", "GG-1"), Opened("f-2", "GG-2"), Opened("f-3", "GG-3"))
            with { Mode = UiMode.Notifications, NotificationAt = 2 };

        await Assert.That(Reducer.Reduce(three, Command.NextNotification).NotificationAt).IsEqualTo(0);
        await Assert.That(Reducer.Reduce(three, Command.PreviousNotification).NotificationAt).IsEqualTo(1);
    }

    [Test]
    public async Task Dismissing_the_last_one_gives_the_console_back()
    {
        var one = Showing(Opened("f-1", "GG-1")) with { Mode = UiMode.Notifications };

        var after = Reducer.Reduce(one, Command.DismissNotification);

        await Assert.That(after.Notifications).IsEmpty();
        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal)
            .Because("a mode about notifications with none left in it is holding the keyboard "
                   + "for nothing.");
    }

    [Test]
    public async Task Dismissing_one_of_several_keeps_the_page_on_something()
    {
        var two = Showing(Opened("f-1", "GG-1"), Opened("f-2", "GG-2"))
            with { Mode = UiMode.Notifications, NotificationAt = 1 };

        var after = Reducer.Reduce(two, Command.DismissNotification);

        await Assert.That(after.Notifications.Select(n => n.FlightId)).IsEquivalentTo(new[] { "f-1" });
        await Assert.That(after.NotificationAt).IsEqualTo(0);
        await Assert.That(after.Mode).IsEqualTo(UiMode.Notifications);
    }

    [Test]
    public async Task Going_to_one_opens_that_flight_where_the_flights_are()
    {
        // THE THREE THINGS A PERSON WOULD PRESS THEIR WAY TO: the flights tab,
        // the cursor on the flight, and the flight open. From either end - the
        // main view's `>` or the focused stack's.
        foreach (var mode in new[] { UiMode.Normal, UiMode.Notifications })
        {
            var from = Showing(Opened("f-1", "GG-1"), Opened("f-2", "GG-2"))
                with { Mode = mode, NotificationAt = 0 };

            var after = Reducer.Reduce(from, Command.GoToNotification);

            await Assert.That(after.ActiveTab).IsEqualTo(TabId.Flights);
            await Assert.That(PaneText.Detailed(after)!.FlightId).IsEqualTo("f-1")
                .Because($"from {mode}: the modal's subject is the flight the notification named.");
            await Assert.That(after.Mode).IsEqualTo(UiMode.FlightDetail);
            await Assert.That(after.Notifications.Select(n => n.FlightId)).IsEquivalentTo(new[] { "f-2" })
                .Because("a notification somebody acted on has been read.");
        }
    }

    [Test]
    public async Task Going_to_a_flight_not_listed_looks_for_it_again()
    {
        // NOTHING TO OPEN, AND STILL SOMEWHERE TO GO. The flight was accepted and
        // has not appeared; going to it lands on the flights tab, where it will
        // be, and the console starts looking for it again rather than leaving
        // the person to press refresh until it does.
        var unlisted = new Notification { Kind = NotificationKind.NotListedYet, FlightId = "f-9" };

        var after = Reducer.Reduce(Showing(unlisted), Command.GoToNotification);

        await Assert.That(after.ActiveTab).IsEqualTo(TabId.Flights);
        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(after.Expecting).IsEquivalentTo(new[]
        {
            new Expectation { Kind = ExpectationKind.FlightAppears, Id = "f-9" },
        });
        await Assert.That(after.Notifications).IsEmpty();
    }

    [Test]
    public async Task Going_to_one_asks_for_the_flight_the_way_opening_it_does()
    {
        // The modal opens on the press and its log arrives as a background
        // read; a command that opened it without being a read would draw a
        // flight whose log never came.
        await Assert.That(ShellCommands.Reads).Contains(Command.GoToNotification);
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.GoToNotification)
            .Because("going to a flight is a keystroke, not a torn-down session.");
    }

    [Test]
    public async Task What_a_notification_says()
    {
        var opened = Showing(Opened("f-2", "GG-2"));
        var several = Showing(Opened("f-1", "GG-1"), Opened("f-2", "GG-2"));
        var unlisted = Showing(new Notification { Kind = NotificationKind.NotListedYet, FlightId = "f-9" });

        await Assert.That(PaneText.NotificationTitle(opened)).IsEqualTo("flight opened");
        await Assert.That(PaneText.NotificationTitle(several)).IsEqualTo("flight opened · 2 of 2")
            .Because("the count only when there is more than one to count.");
        await Assert.That(PaneText.NotificationLines(opened)).IsEquivalentTo(
            new[] { "GG-2 is in the air", "work GG-2" });

        await Assert.That(PaneText.NotificationTitle(unlisted)).IsEqualTo("not listed yet");
        await Assert.That(PaneText.NotificationLines(unlisted)[0]).Contains("accepted")
            .Because("it was accepted - that much is known - and it has not appeared.");
    }

    [Test]
    public async Task The_hint_is_the_keymap_s()
    {
        // ADVERTISED KEYS CANNOT DRIFT FROM LIVE ONES. The corner names its two
        // keys from the same bindings that resolve them.
        var hint = PaneText.NotificationHint(KeymapContext.For(Showing(Opened("f-2", "GG-2"))));

        await Assert.That(hint).IsEqualTo("> go to it · ! notifications");
    }

    private static FlightSummary AFlight(string id, int number, int minutes) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = $"work {number}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = T0.AddMinutes(minutes),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };
}
