using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Terminal release, against something real at last.
/// </summary>
/// <remarks>
/// <para>
/// <c>IUiSession</c> has existed since step 1 and was property-tested over
/// hundreds of generated states in 4b. <b>Neither of those is a real flight.</b>
/// This is real state with real content in the Live pane, a real child process
/// that holds the terminal, and the same state on the other side.
/// </para>
/// <para>
/// It works for the reason it has always claimed to: <c>ConsoleLoop</c> spawns
/// children only BETWEEN sessions, so the terminal is provably free when it
/// does, and the next session is rebuilt from the model alone.
/// </para>
/// </remarks>
public class TakeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static AppState AFlightWorthTaking(string tree) => new()
    {
        Queue =
        [
            new QueueRow
            {
                FlightId = "019ff8aa-1111-7000-8000-000000000001",
                FlightNumber = "GG-42",
                Name = "add the docstring",
                Reason = QueueReason.RunnerOffline,
                Since = T0,
            },
        ],
        // Real content, arrived through the reducer the way runner output does.
        Live =
        [
            new StreamLine { Kind = StreamLineKind.Setup, Text = "session init", At = T0 },
            new StreamLine { Kind = StreamLineKind.Tool, Text = "Read", At = T0 },
            new StreamLine { Kind = StreamLineKind.Text, Text = "I could not stay in scope.", At = T0 },
            new StreamLine { Kind = StreamLineKind.Meta, Text = "loop success", At = T0 },
        ],
        LiveVisible = true,
        Notes = "what I was thinking before any of this",
        TakeableTree = tree,
        TakeSeed = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", tree,
            new LoopDigest
            {
                LoopId = "implement",
                FilesReadNotEdited = ["src/util.py"],
                FilesEdited = ["src/greet.py"],
                Searches = [],
                Errors = [],
                RefusedMoves = [],
                Attempts = 6,
                StopReason = LoopOutcomes.Completed,
            },
            "I edited greet.py and could not satisfy the scope rule.",
            verdict: "violated"),
    };

    private static string Scratch()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "gg-take-" + Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(directory);

        return directory;
    }

    [Test]
    public async Task The_console_comes_back_to_the_state_it_left()
    {
        // THE CRITERION. Real state, real content in the Live pane, a child that
        // held the terminal, and everything still there afterwards.
        var tree = Scratch();

        try
        {
            var before = AFlightWorthTaking(tree);
            var ui = new ScriptedUi(Command.TakeFlight, Command.Quit);

            var after = new ConsoleLoop(ui, new NoEditor(), new RecordingTake(tree)).Run(before);

            await Assert.That(after.Live.Count).IsEqualTo(4)
                .Because("the live pane's content survives a child holding the terminal for "
                       + "minutes, because the model is the only thing that crosses.");
            await Assert.That(after.Notes).IsEqualTo(before.Notes);
            await Assert.That(after.Queue.Single().FlightNumber).IsEqualTo("GG-42");
            await Assert.That(after.LiveVisible).IsTrue();

            // And the second session really was rebuilt from that state.
            await Assert.That(ui.SeenStates.Count).IsEqualTo(2);
            await Assert.That(ui.SeenStates[1].Live.Count).IsEqualTo(4)
                .Because("the session AFTER the takeover was handed the same content, which is what "
                       + "'rebuilt from the model' means.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task The_child_runs_between_sessions_so_the_terminal_is_free()
    {
        // The property the whole design rests on, asserted by ordering rather
        // than by inspection: the session must have ended before the child
        // starts, and the next must not begin until it exits.
        var tree = Scratch();

        try
        {
            var order = new List<string>();
            var ui = new ScriptedUi(Command.TakeFlight, Command.Quit)
            {
                OnRun = () => order.Add("session"),
            };

            new ConsoleLoop(ui, new NoEditor(), new RecordingTake(tree)
            {
                OnTake = () => order.Add("child"),
            }).Run(AFlightWorthTaking(tree));

            await Assert.That(order).IsEquivalentTo((string[])["session", "child", "session"])
                .Because("a child that started while a session was up would be fighting it for the "
                       + "terminal, and the interleaving is the only way to see that.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task What_came_back_is_on_the_state_the_next_session_renders()
    {
        // Anything printed during the takeover is gone: the console is rebuilt
        // after the child exits. So the outcome has to be on the model.
        var tree = Scratch();

        try
        {
            File.WriteAllText(
                TakeoverReturnReader.PathIn(tree),
                """{"flightId":"019ff8aa-1111-7000-8000-000000000001","outcome":"completed","note":"fixed it"}""");

            var before = AFlightWorthTaking(tree);
            var after = new ConsoleLoop(
                new ScriptedUi(Command.TakeFlight, Command.Quit),
                new NoEditor(),
                new RecordingTake(tree)).Run(before);

            await Assert.That(after.LastTakeover!).Contains("completed");
            await Assert.That(after.LastTakeover!).Contains("fixed it");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task A_return_file_for_another_flight_leaves_the_state_saying_so()
    {
        // The malformed case that parses, arriving through the console rather
        // than through the reader in isolation.
        var tree = Scratch();

        try
        {
            File.WriteAllText(
                TakeoverReturnReader.PathIn(tree),
                """{"flightId":"019ff8aa-9999-7000-8000-000000000009","outcome":"completed"}""");

            var before = AFlightWorthTaking(tree);
            var after = new ConsoleLoop(
                new ScriptedUi(Command.TakeFlight, Command.Quit),
                new NoEditor(),
                new RecordingTake(tree)).Run(before);

            await Assert.That(after.LastTakeover!).Contains("019ff8aa-9999");
            await Assert.That(after.Queue.Single().FlightNumber).IsEqualTo("GG-42")
                .Because("the flight is untouched, which is the whole of the promise.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task A_flight_with_no_held_tree_cannot_be_taken_and_says_why()
    {
        // A landed flight's work is on a branch, so there is nothing here to
        // take. Better to say that than to open a session in an empty directory.
        var before = AFlightWorthTaking("/does/not/matter") with { TakeableTree = null };

        var after = new ConsoleLoop(
            new ScriptedUi(Command.TakeFlight, Command.Quit),
            new NoEditor(),
            new RecordingTake("/does/not/matter")).Run(before);

        await Assert.That(after.LastTakeover!).Contains("nothing to take over");
    }

    [Test]
    public async Task The_take_key_is_offered_only_when_there_is_something_to_take()
    {
        // Hints come from the same context dispatch uses, so an advertised key
        // that does nothing is not possible - but only if the context carries
        // the fact.
        var takeable = Keymap.Bindings(new KeymapContext(UiMode.Normal, Takeable: true));
        var not = Keymap.Bindings(new KeymapContext(UiMode.Normal, Takeable: false));

        await Assert.That(takeable.Any(b => b.Command == Command.TakeFlight)).IsTrue();
        await Assert.That(not.Any(b => b.Command == Command.TakeFlight)).IsFalse();
    }

    [Test]
    public async Task A_real_child_holds_the_terminal_and_the_seed_is_waiting_for_it()
    {
        // THE REAL ONE. Everything above uses a double for the spawn, because
        // asserting that .NET starts processes is not a test of this. Here a
        // real process runs, in the flight's tree, with the seed placed for it -
        // and the return file it writes is read back the way a person's would
        // be.
        var tree = Scratch();

        try
        {
            var seed = AFlightWorthTaking(tree).TakeSeed!;

            // A child that does what a person would: look around, then leave a
            // decision behind.
            var script = Path.Combine(tree, "stand-in.sh");
            File.WriteAllText(script,
                "#!/bin/sh\n"
              + "test -f ./src/greet.py || exit 3\n"
              + "cat > ./gg-return.json <<'JSON'\n"
              + $$"""{"flightId":"{{seed.FlightId}}","outcome":"abandoned","note":"needs a human"}"""
              + "\nJSON\n");

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(script,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            Directory.CreateDirectory(Path.Combine(tree, "src"));
            File.WriteAllText(Path.Combine(tree, "src", "greet.py"), "def greet(): pass\n");

            var clipboard = new NoClipboard();
            var result = new TakeSession(script, clipboard).Take(new TakeRequest
            {
                FlightId = seed.FlightId,
                FlightNumber = seed.FlightNumber,
                TreePath = tree,
                Seed = seed,
            });

            await Assert.That(result.Decision).IsNotNull()
                .Because("the child really ran, in the tree, and what it wrote was read back.");
            await Assert.That(result.Decision!.Outcome).IsEqualTo(TakeoverOutcomes.Abandoned);
            await Assert.That(result.Decision.Note).IsEqualTo("needs a human");

            // And the seed reached the person, by the fallback, because this
            // machine's clipboard was refused.
            var placed = result.Placement as SeedPlacement.File;

            await Assert.That(placed).IsNotNull();
            await Assert.That(File.ReadAllText(placed!.Path)).Contains("THE AGENT'S OWN ACCOUNT");
            await Assert.That(result.Held).IsGreaterThan(TimeSpan.Zero);
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    private sealed class NoClipboard : IClipboard
    {
        public string? Copy(string text) => "no clipboard in a test";
    }

    /// <summary>A session that returns the states it was handed, in order.</summary>
    private sealed class ScriptedUi(params Command[] exits) : IUiSession
    {
        private readonly Queue<Command> _exits = new(exits);

        internal List<AppState> SeenStates { get; } = [];

        internal Action? OnRun { get; init; }

        public UiOutcome Run(AppState state)
        {
            OnRun?.Invoke();
            SeenStates.Add(state);

            return new UiOutcome(_exits.Dequeue(), state);
        }
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }

    /// <summary>
    /// A takeover that does everything but spawn a process.
    /// </summary>
    /// <remarks>
    /// The spawn is <c>Process.Start</c> and asserting that .NET starts
    /// processes is not a test of this. What IS tested here is the ordering
    /// around it, the state that survives it, and the return file it reads.
    /// </remarks>
    private sealed class RecordingTake(string tree) : ITakeSession
    {
        internal Action? OnTake { get; init; }

        public TakeResult Take(TakeRequest request)
        {
            OnTake?.Invoke();

            var (decision, diagnosis) = TakeoverReturnReader.Read(
                TakeoverReturnReader.PathIn(tree), request.FlightId);

            return new TakeResult
            {
                Held = TimeSpan.FromMinutes(4),
                Placement = new SeedPlacement.Clipboard(),
                Decision = decision,
                Diagnosis = diagnosis,
            };
        }
    }
}
