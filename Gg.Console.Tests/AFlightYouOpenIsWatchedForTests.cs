using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// Opening a flight names the flight, and the console watches for it rather than
/// re-reading before it can exist.
/// </summary>
/// <remarks>
/// <para>
/// <b>The re-read ran and saw nothing.</b> The door answers 202 as soon as the
/// command is sent; the flight row the list is read from is projected after the
/// command crosses to the Flight context and its event comes back - measured at
/// up to ten seconds. So the reload that followed every opened flight ran first,
/// found the list unchanged, and the row arrived with the next thirty-second
/// tick. It was also the most expensive way to find nothing: every read the boot
/// makes, between one UI session and the next, with nothing on the screen.
/// </para>
/// <para>
/// <b>The id was in hand and thrown away.</b> <c>VerbConsoleActions</c> wrote it
/// into a sentence and returned the sentence. It crosses in a
/// <see cref="Receipt"/> now, and the loop records it as the question to ask.
/// </para>
/// </remarks>
public class AFlightYouOpenIsWatchedForTests
{
    [Test]
    public async Task The_door_names_the_flight_and_the_opening_carries_it()
    {
        var (data, _) = AConsolePlane.Console();
        var actions = new VerbConsoleActions(data, new NeverAsked());

        var opened = actions.Fly("stop the pty test flaking", [], null);

        await Assert.That(opened.Expected?.Id).IsEqualTo(AConsolePlane.Launched)
            .Because("the 202 names the flight, and that id is the only way to ask whether "
                   + "it has appeared.");
    }

    [Test]
    public async Task A_ticket_names_it_too()
    {
        var (data, _) = AConsolePlane.Console();
        var actions = new VerbConsoleActions(data, new NeverAsked());

        var opened = actions.FlyTicket("ado", "18490", [], null);

        await Assert.That(opened.Expected?.Id).IsEqualTo(AConsolePlane.Launched);
    }

    [Test]
    public async Task A_refused_opening_names_nothing()
    {
        // Nothing to watch for - and the loop's answer to that is the re-read it
        // always did, because a refusal is not proof that nothing changed.
        var (data, _) = AConsolePlane.Console();
        var actions = new VerbConsoleActions(data, new NeverAsked());

        var opened = actions.Fly("ado#", [], null);

        await Assert.That(opened.Expected?.Id).IsNull();
    }

    [Test]
    public async Task Opening_a_flight_from_the_editor_watches_for_it_and_does_not_reload()
    {
        var reloads = 0;

        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.OpenFlight),
                new ConsoleDoubles.Writes("stop the pty test flaking"),
                actions: new ConsoleDoubles.Records(),
                reload: current =>
                {
                    reloads++;
                    return current;
                })
            .Run(new AppState());

        await Assert.That(final.Expecting).IsEquivalentTo(new[]
        {
            new Expectation { Kind = ExpectationKind.FlightAppears, Id = ConsoleDoubles.Records.Opened },
        })
            .Because("the flight the door named is the question the console now asks.");
        await Assert.That(reloads).IsEqualTo(0)
            .Because("a reload the moment the door answers runs before the row exists: every "
                   + "read the boot makes, with nothing on the screen, to find nothing.");
    }

    [Test]
    public async Task Nothing_written_watches_for_nothing()
    {
        // An empty buffer opens nothing, so there is no id and no question.
        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.OpenFlight),
                new ConsoleDoubles.Writes("   "),
                actions: new ConsoleDoubles.Records())
            .Run(new AppState());

        await Assert.That(final.Expecting).IsEmpty();
    }

    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("nothing about opening a flight asks for a secret.");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("nothing about opening a flight asks for a line.");
    }
}
