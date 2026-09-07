using System.Text;

namespace Gg.Console.Tests;

/// <summary>
/// The editor handoff, run inside a pseudo-terminal gg keeps a bar on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same <see cref="IEditorSession"/>, so nothing above it changes.</b>
/// What a caller wants from the editor is unchanged — text in, text out, a real
/// process in between — and the difference is only that gg mediates the terminal
/// rather than handing it over. Every seam that would have to move if this were
/// a new interface is a seam that would have to be tested again.
/// </para>
/// <para>
/// <b>It falls back, and the fallback is the point of testing it.</b> gg runs
/// where there is no controlling terminal, and on Windows, where there is no
/// <c>/dev/tty</c> to open at all. A hosted editor that threw in those places
/// would take away an editor that works today for a bar that cannot be drawn.
/// </para>
/// </remarks>
public class PtyEditorSessionTests
{
    /// <summary>A real external "editor": a shell script that appends to its file.</summary>
    private static string FakeEditor(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-fake-editor-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\n" + script);
        return path;
    }

    /// <summary>A terminal made by the test, and everything gg painted on it.</summary>
    private sealed class Owned : IHostTerminal, IDisposable
    {
        private readonly PseudoTerminal _pty = PseudoTerminal.Open();
        private readonly StringBuilder _painted = new();
        private readonly Lock _lock = new();
        private FileStream? _keystrokes;

        internal bool Opened => _pty.Opened;

        public int Columns => 80;

        public int Rows => 24;

        public int Descriptor => _pty.Slave;

        public Stream Keystrokes => _keystrokes ??= _pty.ReadSlave();

        public void Paint(string frame)
        {
            lock (_lock)
            {
                _painted.Append(frame);
            }
        }

        internal string Painted
        {
            get
            {
                lock (_lock)
                {
                    return _painted.ToString();
                }
            }
        }

        public void Dispose()
        {
            _keystrokes?.Dispose();
            _pty.Dispose();
        }
    }

    [Test]
    public async Task What_the_editor_wrote_comes_back()
    {
        var editor = FakeEditor("printf 'edited by pid %s\\n' $$ >> \"$1\"\n");
        using var terminal = new Owned();
        await Assert.That(terminal.Opened).IsTrue();

        try
        {
            var edited = new PtyEditorSession($"/bin/sh {editor}", () => terminal)
                .Edit("original text\n");

            await Assert.That(edited).StartsWith("original text\n");
            await Assert.That(edited).Contains("edited by pid ")
                .Because("a real process really opened the file and really wrote to it - "
                       + "hosting it changes who owns the terminal, not what an editor is.");
        }
        finally
        {
            File.Delete(editor);
        }
    }

    [Test]
    public async Task The_bar_stays_on_screen_while_the_editor_has_it()
    {
        var editor = FakeEditor("printf 'x' >> \"$1\"\n");
        using var terminal = new Owned();
        await Assert.That(terminal.Opened).IsTrue();

        try
        {
            new PtyEditorSession($"/bin/sh {editor}", () => terminal).Edit("");

            await Assert.That(terminal.Painted).Contains("gg", StringComparison.Ordinal)
                .Because("the top row is the only surface gg still owns once the editor has "
                       + "the screen, and it is the whole reason for hosting rather than "
                       + "handing over.");
        }
        finally
        {
            File.Delete(editor);
        }
    }

    [Test]
    public async Task Without_a_terminal_it_still_edits()
    {
        // gg runs in CI, behind a pipe, and on Windows, where there is no
        // /dev/tty to open. A hosted editor that threw in any of those would
        // take away an editor that works today in exchange for a bar that
        // cannot be drawn on a terminal that is not there.
        var editor = FakeEditor("printf 'edited without a tty\\n' >> \"$1\"\n");

        try
        {
            var edited = new PtyEditorSession($"/bin/sh {editor}", () => null)
                .Edit("original text\n");

            await Assert.That(edited).StartsWith("original text\n");
            await Assert.That(edited).Contains("edited without a tty");
        }
        finally
        {
            File.Delete(editor);
        }
    }

    [Test]
    public async Task The_working_file_is_deleted_however_the_editor_ended()
    {
        // An editor a person abandoned is the ordinary case, not an error - and
        // the file it was given holds whatever they were writing, in a directory
        // everybody on the machine can read.
        foreach (var (ending, script) in ((string, string)[])
                 [("saved", "printf 'kept\\n' >> \"$1\"\n"),
                  ("abandoned", "exit 1\n"),
                  ("killed outright", "kill -9 $$\n")])
        {
            var editor = FakeEditor(script);
            using var terminal = new Owned();
            await Assert.That(terminal.Opened).IsTrue();

            // A DIRECTORY THIS TEST OWNS. A first version counted gg-notes files
            // in the shared temp directory, which is also where every other test
            // that edits something is putting one - so the count moved under it
            // while it ran and the assertion failed against work that was not
            // its own.
            var notes = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(), "gg-notes-test-" + Guid.NewGuid().ToString("N")[..8]));

            try
            {
                new PtyEditorSession($"/bin/sh {editor}", () => terminal, notesIn: notes.FullName)
                    .Edit("secret draft\n");

                await Assert.That(notes.GetFiles()).IsEmpty()
                    .Because($"an editor that was {ending} still leaves gg holding the file, "
                           + "and it holds whatever a person was writing.");
            }
            finally
            {
                File.Delete(editor);
                notes.Delete(recursive: true);
            }
        }
    }

    [Test]
    public async Task The_console_is_built_with_the_hosted_editor()
    {
        // THE DEFECT THIS EXISTS BECAUSE OF. In the spike the agent host was
        // wired into a branch that could not be reached - TakeableTree was
        // always null - so it was reported working while the thing it replaced
        // ran instead. "It opened vim again." A host nothing constructs is a
        // host nothing tests, and the construction site is the only place that
        // can say which one runs.
        var program = ConsoleSource.Text("Gg.Cli", "Program.cs");

        await Assert.That(program).Contains("PtyEditorSession", StringComparison.Ordinal)
            .Because("this is the live construction site for the console's editor port.");

        await Assert.That(program).DoesNotContain("new EditorSession()", StringComparison.Ordinal)
            .Because("the unhosted one is now reached through the fallback, which decides "
                   + "whether there is a terminal to host on - and two construction sites "
                   + "means the decision is made twice and can disagree.");
    }
}
