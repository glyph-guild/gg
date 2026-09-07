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

    [Test]
    public async Task What_the_editor_wrote_comes_back()
    {
        var editor = FakeEditor("printf 'edited by pid %s\\n' $$ >> \"$1\"\n");
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

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
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

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
            using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

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

    /// <summary>An editor that does not spawn anything, and says so.</summary>
    private sealed class Stub(string wrote) : IEditorSession
    {
        public string Edit(string initialText) => wrote;
    }

    [Test]
    public async Task A_machine_missing_the_native_library_still_edits()
    {
        // WHAT THE PACKAGE ACTUALLY COSTS. Porta.Pty P/Invokes libporta_pty for
        // pty_spawn, pty_waitpid and eight more, and that file ships BESIDE gg
        // rather than inside it - a second one after the onigwrap that already
        // arrives with Terminal.Gui. If whatever installs gg moves the binary and
        // not the directory, nothing says so at build time and nothing says so at
        // startup, because .NET resolves a P/Invoke on first call: the first
        // anybody hears of it is the console dying on the key they just pressed.
        //
        // A machine that cannot host still has an editor. It has had one all
        // along.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

        var edited = new PtyEditorSession(
            "vi",
            () => terminal,
            unhosted: new Stub("what the unhosted editor wrote"),
            host: (_, _, _, _, _, _) => throw new DllNotFoundException("libporta_pty"))
            .Edit("original text\n");

        await Assert.That(edited).IsEqualTo("what the unhosted editor wrote")
            .Because("the bar is a nicety and the editor is not, so losing the first must "
                   + "never cost the second.");
    }

    [Test]
    public async Task Falling_back_is_said_out_loud_rather_than_guessed_at()
    {
        // A SILENT FALLBACK MAKES THE FEATURE LOOK UNBUILT. The release tarball
        // carries `gg` and nothing else - `tar -czf ... -C out gg` - so a person
        // who installs the documented way has no libporta_pty at all. Their
        // editor opens, works, and never has a bar, and there is no way for them
        // to tell "this machine cannot host" from "this was never finished".
        //
        // One line, before the editor takes the screen, which is the only moment
        // anything gg says can still be read.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

        var said = new List<string>();

        new PtyEditorSession(
            "vi",
            () => terminal,
            unhosted: new Stub("edited"),
            host: (_, _, _, _, _, _) => throw new DllNotFoundException("libporta_pty"),
            say: said.Add)
            .Edit("original text\n");

        await Assert.That(said).IsNotEmpty()
            .Because("a person whose editor silently lost its bar has no way to tell a "
                   + "machine that cannot host from a feature nobody built.");

        var told = string.Join(" ", said);

        await Assert.That(told).Contains("libporta_pty", StringComparison.Ordinal)
            .Because("naming the file is what makes this actionable rather than a shrug - it "
                   + "is a missing file, and somebody can go and get it.");
        await Assert.That(told).Contains("editor", StringComparison.OrdinalIgnoreCase)
            .Because("and saying what still works matters more than saying what did not.");
    }

    [Test]
    public async Task Nothing_is_said_when_there_was_never_a_terminal_to_host_on()
    {
        // THE CASE THAT MUST STAY QUIET. Running under CI, behind a pipe, or on
        // Windows is not a fault and there is nobody at a keyboard to tell. A
        // warning on every piped invocation is noise that teaches people to
        // ignore the one above.
        var editor = FakeEditor("printf 'x' >> \"$1\"\n");
        var said = new List<string>();

        try
        {
            new PtyEditorSession($"/bin/sh {editor}", () => null, say: said.Add).Edit("");

            await Assert.That(said).IsEmpty()
                .Because("no terminal is an ordinary condition, not a degraded one.");
        }
        finally
        {
            File.Delete(editor);
        }
    }

    [Test]
    public async Task An_editor_that_is_simply_broken_is_not_swallowed()
    {
        // THE OTHER HALF, AND THE REASON THE CATCH IS NAMED RATHER THAN BARE. A
        // fallback that ran on any failure would turn "your $EDITOR is not
        // installed" into a silent second attempt at the same thing, and a
        // person would be left with an editor that never opens and no reason
        // given. Only the native library is a reason to stop hosting; everything
        // else is something they need to be told.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

        var session = new PtyEditorSession(
            "vi",
            () => terminal,
            unhosted: new Stub("the fallback must not have run"),
            host: (_, _, _, _, _, _) => throw new InvalidOperationException("no such editor"));

        await Assert.That(() => session.Edit("original text\n"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task The_terminal_is_closed_even_if_the_draft_cannot_be_written()
    {
        // A REAL TERMINAL IS A HELD RESOURCE, not a value: an open /dev/tty and
        // a signal registration. The draft is written before the block that
        // closes it, so a disk that is full, a directory that has gone, or a
        // path that was never there leaks both - and the console goes on running
        // afterwards, so the leak accumulates one per attempt.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

        var nowhere = Path.Combine(
            Path.GetTempPath(), "gg-not-a-directory-" + Guid.NewGuid().ToString("N")[..8]);

        var session = new PtyEditorSession(
            "vi", () => terminal, unhosted: new Stub("unreached"), notesIn: nowhere);

        await Assert.That(() => session.Edit("a draft nobody will read")).Throws<DirectoryNotFoundException>()
            .Because("a draft that cannot be written is a real failure and the person has to "
                   + "hear about it - this is about what is left behind on the way out.");

        await Assert.That(terminal.Disposed).IsTrue()
            .Because("the descriptor and the SIGWINCH registration are gg's to close, and "
                   + "nothing else will.");
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
