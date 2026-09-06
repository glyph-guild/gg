using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A flight you can see is a flight you can look into: the cursor lands on one
/// and enter opens what is known about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The flights tab could list a flight and go no further.</b> It shows what
/// every flight did - GG-52 landed with a blocked loop - and the next question
/// a person has is always the same one: what happened, in order. That is the
/// flight's log, which the boot already fetched.
/// </para>
/// <para>
/// <b>A modal rather than another tab.</b> Looking into one flight is a
/// question with an answer and a way out, which is what a modal is for; a tab
/// would be a ninth thing on the bar that is only ever about whatever was
/// last selected. It owns the keyboard while it is open, like every other
/// modal here, and <c>KeymapTests</c> subjects it to that discipline the moment
/// the mode exists.
/// </para>
/// <para>
/// <b>And the cursor belongs to the list that has the screen.</b> Under one
/// shared region <c>Moved</c> could ask which flags were set; a view takes the
/// whole screen now, so what decides is which tab is showing - a latent defect
/// the tab work introduced, since a flag means OPEN now rather than SHOWING.
/// </para>
/// </remarks>
public class EnterOpensWhatTheCursorIsOnTests
{
    private static FlightSummary AFlight(string number, DateTimeOffset created) => new()
    {
        FlightId = $"01a0776a-cacb-76dc-b444-2b7031{number}",
        FlightNumber = number,
        Name = $"work for {number}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = $"work for {number}" },
        CreatedAt = created,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Landed,
        Facts = [],
    };

    private static AppState Listing() => new()
    {
        ActiveTab = TabId.Flights,
        Flights = new FlightList
        {
            Flights =
            [
                AFlight("GG-51", new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero)),
                AFlight("GG-52", new DateTimeOffset(2026, 9, 6, 15, 51, 0, TimeSpan.Zero)),
            ],
        },
        Logs = new Dictionary<string, FlightLog>(StringComparer.Ordinal)
        {
            ["01a0776a-cacb-76dc-b444-2b7031GG-52"] = new FlightLog
            {
                FlightId = "01a0776a-cacb-76dc-b444-2b7031GG-52",
                FlightNumber = "GG-52",
                Entries =
                [
                    new FlightLogEntry
                    {
                        Kind = "lease-granted",
                        At = new DateTimeOffset(2026, 9, 6, 15, 51, 29, TimeSpan.Zero),
                        Detail = "granted to gg-runner 01a06572",
                    },
                ],
            },
        },
    };

    [Test]
    public async Task Enter_on_a_flight_opens_what_is_known_about_it()
    {
        var opened = Reducer.FlightShown(Listing());

        await Assert.That(opened.Mode).IsEqualTo(UiMode.FlightDetail);
        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, new KeymapContext(UiMode.Normal)))
            .IsEqualTo(Command.ShowFlight)
            .Because("enter is the key a person presses on a row without being told to.");

        // THROUGH THE SHELL, because the modal reads. The reducer's arm for this
        // command changes nothing on purpose - ShellHandledTests forbids a shell
        // command with a second, local effect - so the named method above is
        // what the loop calls once the log has arrived.
        await Assert.That(ShellCommands.Handled).Contains(Command.ShowFlight);
        await Assert.That(Reducer.Reduce(Listing(), Command.ShowFlight).Mode)
            .IsEqualTo(UiMode.Normal)
            .Because("opening it from the reducer would open it whether or not the log was "
                   + "read, over a pane that then never corrects itself.");
    }

    [Test]
    public async Task It_is_about_the_flight_under_the_cursor_and_carries_its_log()
    {
        // NEWEST FIRST is what the pane shows, so the cursor at rest is on the
        // newest flight - and the modal has to be about the row a person is
        // looking at rather than the first one in the list the boot returned.
        var opened = Reducer.FlightShown(Listing());
        var modal = PaneText.Modal(opened);

        await Assert.That(modal).Contains("GG-52", StringComparison.Ordinal);
        await Assert.That(modal).DoesNotContain("GG-51", StringComparison.Ordinal)
            .Because("one flight, the one under the cursor. Modal:\n" + modal);
        await Assert.That(modal).Contains("lease-granted", StringComparison.Ordinal)
            .Because("the log is the answer to the question a person opened this to ask. "
                   + "ConsoleLoop reads it before calling this, which is why the modal can "
                   + "assume it is there. Modal:\n" + modal);
    }

    [Test]
    public async Task Nothing_opens_when_the_list_is_empty()
    {
        // ARTICLE XI. A key that appears to work is worse than one that is not
        // offered: the modal would open onto nothing and the way out would be
        // the only thing in it.
        var empty = new AppState { ActiveTab = TabId.Flights };

        await Assert.That(Reducer.FlightShown(empty).Mode)
            .IsEqualTo(UiMode.Normal)
            .Because("there is no flight under the cursor, so there is nothing to open.");

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, new KeymapContext(UiMode.Normal)))
            .IsNotNull()
            .Because("the key stays bound, because whether there is a row is not the "
                   + "keymap's question - the reducer answers it, once.");
    }

    [Test]
    public async Task Esc_is_the_way_out_and_the_only_one()
    {
        var opened = Reducer.FlightShown(Listing());
        var context = new KeymapContext(UiMode.FlightDetail);

        await Assert.That(Keymap.EscapeHatch(context)).IsEqualTo(KeyStroke.Esc);
        await Assert.That(Reducer.Reduce(opened, Command.CloseModal).Mode)
            .IsEqualTo(UiMode.Normal);

        // AND IT OWNS THE KEYBOARD. Reading it should not be able to act on the
        // flight it is about - the rule every modal here follows, and the
        // generated tests in KeymapTests hold it the moment the mode exists.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), context)).IsNull()
            .Because("`d` decides a gate in Normal mode, and a person reading a log has not "
                   + "asked to decide anything.");
    }

    [Test]
    public async Task The_cursor_belongs_to_the_list_that_has_the_screen()
    {
        // THE LATENT DEFECT THE TAB WORK LEFT. Moved asked which flags were
        // set, which meant SHOWING under one shared region and means OPEN under
        // tabs - so j and k moved the repository cursor while a person was
        // looking at the queue.
        var state = Listing() with { RepositoriesVisible = true, SelectedRow = 0 };

        var moved = Reducer.Reduce(state, Command.SelectNext);

        await Assert.That(moved.FlightSelected).IsEqualTo(1)
            .Because("the flights tab has the screen, so the flights cursor is the one that "
                   + "moves.");
        await Assert.That(moved.RepositorySelected).IsEqualTo(0)
            .Because("the repositories are open behind it and nobody is looking at them.");
        await Assert.That(moved.SelectedRow).IsEqualTo(0)
            .Because("and the queue's own cursor stays where the person left it.");
    }
}
