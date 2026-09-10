using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// The pane advances during a session, and the exception that allows it is narrow.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the console's first mid-session read, and the exception will be
/// quoted.</b> Every effect in this console happens between sessions with the
/// terminal provably free; streaming cannot work that way, because a pane that
/// advances only when a session tears down and rebuilds advances when you press
/// a key, which is a worse <c>gg log</c>.
/// </para>
/// <para>
/// So the rule is not "sessions may do I/O". It is: <b>a UI session may advance
/// state from a local file whose path the console already holds, and may not
/// make a network call, resolve a credential, or spawn a process.</b> The scope
/// is asserted below rather than described, so the next feature that wants a
/// network call in a session has to argue for its own exception instead of
/// inheriting this one.
/// </para>
/// </remarks>
public class LiveStreamingTests
{
    private sealed class ScriptedSource(bool exists, params StreamLine[][] batches) : ILiveSource
    {
        private readonly Queue<StreamLine[]> _batches = new(batches);

        public int Reads { get; private set; }

        public bool Exists => exists;

        public IReadOnlyList<StreamLine> Read()
        {
            Reads++;
            return _batches.Count > 0 ? _batches.Dequeue() : [];
        }
    }

    private sealed class ThrowingSource : ILiveSource
    {
        public bool Exists => true;

        public IReadOnlyList<StreamLine> Read() =>
            throw new IOException("the file went away mid-session");
    }

    private static StreamLine Line(string text) =>
        new() { Kind = StreamLineKind.Text, Text = text, At = DateTimeOffset.UnixEpoch };

    private static AppState Watching() => new()
    {
        LiveVisible = true,
        Queue =
        [
            new QueueRow
            {
                FlightId = "f1",
                FlightNumber = "GG-1",
                Name = "a flight",
                Reason = QueueReason.AwaitingDecision,
                Since = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("no Gg.sln above the tests");
    }

    // ---- S31.4-01: it advances without a keypress ----

    [Test]
    public async Task A_tick_folds_new_lines_in_without_any_command()
    {
        // THE FEATURE, at the seam the timer drives. No Command is reduced and
        // no key is pressed: the state advances because time passed.
        var tails = new LiveTails(_ => new ScriptedSource(
            exists: true, [Line("first")], [Line("second")]));

        var state = tails.Advance(Watching());
        state = tails.Advance(state);

        await Assert.That(state.Live.Select(l => l.Text))
            .IsEquivalentTo((string[])["first", "second"])
            .Because("a pane that only advances on a keypress is a worse gg log, which is "
                   + "what this step exists to stop being.");
        await Assert.That(state.Silence).IsEqualTo(LiveSilence.Speaking);
    }

    // ---- S31.4-02: the scope of the exception, structurally ----

    [Test]
    public async Task The_session_reaches_a_local_file_and_nothing_else()
    {
        // ASSERTED OVER WHAT THE SESSION MAY REACH. A comment saying "local
        // files only" is a comment; this is the thing that fails when somebody
        // adds an HttpClient to a screen because it was convenient.
        var forbidden = new (string Name, Regex Pattern)[]
        {
            ("a network call", new Regex(@"\bHttpClient\b|\bWebRequest\b|\bSocket\b|\bHttpMessage")),
            ("a child process", new Regex(@"\bProcess\.Start\b|\bProcessStartInfo\b")),
            ("a credential", new Regex(@"CredentialStore|SessionStore|SessionToken|ControlPlaneClient")),
        };

        // TWO EXCEPTIONS, GRANTED AND WRITTEN DOWN HERE RATHER THAN WHERE THEY
        // ARE USED. Both belong to one field - the airspace path - and both
        // exist because that field is the one place a person is holding a value
        // gg would throw away by releasing the terminal to go and get it.
        //
        // THE FIRST: THE CLIPBOARD.
        //
        // Reading the clipboard is a child process on every platform this ships
        // to - ConsoleLink says exactly that about its own copy: "Both spawn a
        // child, which is why neither is a session's." Pasting into the
        // airspace field does it during a session anyway, deliberately: a path
        // is most often already on the clipboard, and a terminal-release round
        // trip in the middle of editing one field would throw away what is
        // typed in it.
        //
        // WHY IT IS RECORDED IN THIS TEST. The spawn happens inside
        // Terminal.Gui's IClipboard, so the scan below finds no Process.Start
        // in this console's own files and passes - which would make the guard
        // read as compliance while the behaviour is the thing it forbids. An
        // exception nobody told the guard about is worse than no guard, because
        // it looks like one.
        //
        // WHAT IT IS NOT. It is scoped to a clipboard READ, in one field, by
        // one key. It grants nothing about the network, nothing about a
        // credential, and no second spawn: the console's other clipboard use -
        // copying a sign-in link - stays the shell's, and that is deliberate,
        // because the thing it copies is a single-use code.
        //
        // THE SECOND: A DIRECTORY PICKER, AND IT IS THE NARROWER OF THE TWO.
        //
        // Terminal.Gui's FileDialog reads directory listings a person walks as
        // they walk them, which is more than "a local file whose path the
        // console already holds" - the scope this rule states. It is granted
        // for the same reason and on the same field: browsing to a directory is
        // how somebody finds a path they cannot type from memory, and a
        // terminal-release round trip would discard the half-typed one.
        //
        // WHY IT IS THE CHEAPER EXCEPTION, and worth separating from the first:
        // it spawns nothing and reaches no network. A FileDialog is views and
        // System.IO, so the scan below still holds over the file that opens it -
        // which is the opposite of the clipboard's situation, where the spawn
        // hides one layer down and the scan is the thing being fooled.
        //
        // WHY IT IS STILL WRITTEN DOWN. Without it, the next reader infers from
        // the clipboard paragraph that arbitrary filesystem reads were always
        // inside the rule, when what the rule allows is one file whose path was
        // already known. Recording it keeps the boundary where it is: this
        // reads directories somebody navigates to, it writes nothing, and it
        // opens no file it finds.
        //
        // The next feature that wants more argues for its own exception here.
        // It does not inherit either of these.

        var sessionSources = new[]
        {
            Path.Combine(RepoRoot(), "Gg.Console", "TerminalGuiSession.cs"),
            Path.Combine(RepoRoot(), "Gg.Console", "Views", "ConsoleScreen.cs"),
            Path.Combine(RepoRoot(), "Gg.Console", "LiveTails.cs"),
            Path.Combine(RepoRoot(), "Gg.Console", "LiveTail.cs"),

            // THE SECOND THING A SESSION READS, and it is the same kind of
            // thing: the log a runner started from this console writes, at a
            // path this console chose. The exception was written for a file
            // whose path the console holds and this is one - what it must not
            // become is the door somebody spawns through, which is why starting
            // and stopping the runner live behind a different interface and in
            // the shell.
            Path.Combine(RepoRoot(), "Gg.Console", "RunnerLog.cs"),
        };

        foreach (var file in sessionSources)
        {
            await Assert.That(File.Exists(file)).IsTrue()
                .Because($"the scan names {Path.GetFileName(file)} and a scan over a file that "
                       + "moved is a scan over nothing.");

            var source = await File.ReadAllTextAsync(file);

            foreach (var (name, pattern) in forbidden)
            {
                await Assert.That(pattern.IsMatch(source)).IsFalse()
                    .Because($"{Path.GetFileName(file)} runs inside a UI session, and the "
                           + $"exception that lets it read at all is scoped to a local file. "
                           + $"It may not make {name}. The next feature that wants one has to "
                           + "argue for its own exception rather than inherit this.");
            }
        }
    }

    [Test]
    public async Task The_scan_would_notice_the_thing_it_forbids()
    {
        // The planted twin, on the guard's own question rather than by putting
        // an HttpClient into a screen to see what happens.
        var pattern = new Regex(@"\bHttpClient\b|\bWebRequest\b|\bSocket\b|\bHttpMessage");

        await Assert.That(pattern.IsMatch("private readonly HttpClient _http = new();")).IsTrue()
            .Because("if the pattern cannot see the thing it forbids, the assertions above "
                   + "pass over a session that does anything at all.");
    }

    // ---- S31.4-03: the state stays plain data ----

    [Test]
    public async Task The_state_carries_no_reader_no_handle_and_no_timer()
    {
        // AppState is serialized under GG_STATE_DUMP and rebuilt from JSON, so a
        // handle in it is a session that cannot be torn down - the one thing
        // terminal release cannot survive.
        var offenders = typeof(AppState).GetProperties()
            .Where(p => p.PropertyType.Namespace?.StartsWith("System.IO", StringComparison.Ordinal) == true
                     || p.PropertyType.Name.Contains("Timer", StringComparison.Ordinal)
                     || p.PropertyType.Name.Contains("Stream", StringComparison.Ordinal)
                        && p.PropertyType.Name != "StreamLine"
                     || typeof(IDisposable).IsAssignableFrom(p.PropertyType))
            .Select(p => $"{p.Name}: {p.PropertyType.Name}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the model is plain data and the tick did not change that: LiveTails is a "
                   + "collaborator owned outside every UI lifetime, not something the state or "
                   + "the session accumulated. Found: " + string.Join(", ", offenders));
    }

    // ---- S31.4-04: a read that throws does not take the session down ----

    [Test]
    public async Task A_read_that_throws_stops_the_tail_and_says_so()
    {
        var tails = new LiveTails(_ => new ThrowingSource());

        var state = tails.Advance(Watching());

        await Assert.That(tails.Faults).IsEqualTo(1)
            .Because("it is counted rather than swallowed, so a tail that keeps dying is "
                   + "visible rather than merely quiet.");
        await Assert.That(state.Silence).IsEqualTo(LiveSilence.Stopped);
        await Assert.That(PaneText.Live(state)).Contains("The tail stopped")
            .Because("a reader that died quietly looks exactly like a flight that went quiet, "
                   + "and those want opposite reactions from a person.");
        await Assert.That(PaneText.Live(state)).Contains("the flight is unaffected")
            .Because("the person reading this needs to know their work is fine and only the "
                   + "view broke.");
    }

    // ---- S31.4-05 and S31.4-06: it costs nothing when nobody is watching ----

    [Test]
    public async Task A_detached_pane_is_not_polled_at_all()
    {
        var source = new ScriptedSource(exists: true, [Line("never read")]);
        var tails = new LiveTails(_ => source);

        var state = tails.Advance(Watching() with { LiveVisible = false });

        await Assert.That(source.Reads).IsEqualTo(0)
            .Because("most flights write nothing most of the time, and a poll per frame on a "
                   + "console nobody attached is a laptop fan. Detaching is also how somebody "
                   + "who does not want this makes it stop.");
        await Assert.That(state.Silence).IsEqualTo(LiveSilence.NotAttached);
    }

    [Test]
    public async Task A_look_that_finds_nothing_changes_nothing()
    {
        // The tick re-renders only when the state actually moved. A repaint per
        // look on a silent flight is the redraw cost that makes a console feel
        // broken, and it is the reason the screen compares before rendering.
        var tails = new LiveTails(_ => new ScriptedSource(exists: true));

        var first = tails.Advance(Watching());
        var second = tails.Advance(first);

        await Assert.That(second).IsEqualTo(first)
            .Because("an unchanged state is what lets the screen skip the repaint, and four "
                   + "looks a second repainting an empty pane is the fan again.");
    }
}
