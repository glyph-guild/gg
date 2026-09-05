using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A refresh replaces what the control plane answers and keeps the rest.
/// </summary>
/// <remarks>
/// <para>
/// <b>The refresh rebuilt the model instead of threading it.</b>
/// <c>ConsoleStart.LoadAsync</c> starts from <c>new AppState()</c> - correct
/// for a boot, and step 3 then made it the refresh as well. Every field the
/// boot does not read reset to a default on every write, because
/// <c>ConsoleLoop.Reloaded</c> carried six of them forward by name and there
/// are more than six.
/// </para>
/// <para>
/// <b>Three symptoms, one cause.</b> The browse pane closed under the person
/// using it. The sentence saying what their keypress did was discarded by the
/// re-read that keypress triggered. And a refresh that could not reach the
/// control plane emptied the console, because <c>LoadAsync</c> catches its own
/// failure and answers with an empty model - so <c>Reloaded</c>'s catch block,
/// and the paragraph promising the last good model is kept, never ran.
/// </para>
/// <para>
/// <b>Lengthening the list would have been the same defect one slice later.</b>
/// A whitelist of what must survive grows every time anybody adds a field and
/// forgetting one is silent. The model is threaded instead: the loader is given
/// what the person has, and assigns the read plane onto it.
/// </para>
/// </remarks>
public class RefreshThreadsTheModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A control plane at a port nothing listens on.</summary>
    private static ConsoleData Unreachable()
    {
        var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var client = new ControlPlaneClient(http);
        var sessions = new NoSession();

        return new ConsoleData(
            new FlightCommands(client, sessions),
            new CredentialCommands(client, sessions, new NoStore(), new NeverAsked()),
            new TakeCommands(client, sessions),
            new IdentityCommands(client, sessions));
    }

    [Test]
    public async Task A_refresh_that_failed_keeps_the_last_good_model()
    {
        // THE PARAGRAPH THAT WAS NOT TRUE. ConsoleLoop.Reloaded says a failure
        // keeps the last good model and catches four exception types to do it.
        // LoadAsync catches the same four FIRST and answers with
        // `new AppState { Diagnosis = ... }`, so the catch never ran and the
        // console emptied. Emptying is the worst answer: the person loses what
        // they had and cannot tell whether the work went away.
        var held = new AppState
        {
            Queue = [Row("a", 1)],
            Principal = "somebody",
            BrowseVisible = true,
            LastFlightOpened = "Opened f-1.",
        };

        var after = await ConsoleStart.LoadAsync(Unreachable(), "somebody", held);

        await Assert.That(after.Queue.Count).IsEqualTo(1)
            .Because("what was there is still true until something better is known.");
        await Assert.That(after.BrowseVisible).IsTrue();
        await Assert.That(after.LastFlightOpened).IsEqualTo("Opened f-1.");
        await Assert.That(after.Diagnosis).IsNotNull()
            .Because("kept, and SAID - a console that forgets why it is stale looks like it "
                   + "is working.");
    }

    [Test]
    public async Task A_refresh_answers_with_the_state_it_was_given_and_nothing_invented()
    {
        // THE RATCHET, AND IT NEEDS NO LIST. Every field of a generated model
        // survives a refresh that read nothing, so the next field somebody adds
        // to AppState inherits the property instead of needing an entry
        // somewhere. What the control plane DID answer is covered by the boot's
        // own tests; what it did not answer may not be invented here.
        var data = Unreachable();

        for (var seed = 1; seed <= 12; seed++)
        {
            var held = StateGenerator.Next(new Random(seed));
            var after = await ConsoleStart.LoadAsync(data, held.Principal, held);

            // Diagnosis is the one field a failed read is entitled to write.
            var expected = held with { Diagnosis = after.Diagnosis };

            await Assert.That(after).IsEqualTo(expected)
                .Because($"seed {seed}: a refresh that reached nothing has nothing to say "
                       + "about any field, so every one of them is still the person's.");
        }
    }

    private static QueueRow Row(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = "waiting",
        Reason = QueueReason.AwaitingDecision,
        Since = T0,
    };

    private sealed class NoSession : ISessionStore
    {
        public StoredSession? Read() => null;

        public void Write(StoredSession session) { }

        public void Clear() { }
    }

    private sealed class NoStore : ICredentialStore
    {
        public string Root => "(no store)";

        public string Protection => "nothing is stored";

        public string PathFor(string locator) => throw new InvalidOperationException("no store");

        public void Write(string locator, string secret) =>
            throw new InvalidOperationException("no store");

        public string? Read(string locator) => null;

        public bool Remove(string locator) => false;
    }

    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");
    }
}
