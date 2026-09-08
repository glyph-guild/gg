namespace Gg.Console;

/// <summary>
/// Runs <c>$EDITOR</c> inside a pseudo-terminal gg owns, keeping a gg bar on the
/// top row, and answers with what was written.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same port as <see cref="EditorSession"/>.</b> Text in, text out, a real
/// process in between — what changes is that gg mediates the terminal rather
/// than handing it over, so a person can still see where they are while an
/// editor has the screen.
/// </para>
/// <para>
/// <b>It falls back rather than throwing.</b> gg runs in CI, behind a pipe, and
/// on Windows, where there is no <c>/dev/tty</c> to open. Refusing to edit in
/// those places would take away an editor that works today in exchange for a bar
/// that cannot be drawn on a terminal that is not there. The decision is made
/// once, here, because two places that decide it will eventually disagree.
/// </para>
/// </remarks>
public sealed class PtyEditorSession : IEditorSession
{
    private readonly string _editorCommand;
    private readonly Func<IHostTerminal?> _terminal;
    private readonly IEditorSession _unhosted;
    private readonly string _bar;
    private readonly string _notesIn;
    private readonly HostRun _host;
    private readonly Action<string> _say;

    /// <param name="editorCommand">
    /// The editor, as a command line. Defaults to <c>$EDITOR</c>, then to
    /// <c>vi</c>.
    /// </param>
    /// <param name="terminal">
    /// Where to get a terminal to host on, answering null when there is none.
    /// A function rather than a terminal, because the console asks between UI
    /// sessions and what is true then is not what was true at construction.
    /// </param>
    /// <param name="unhosted">
    /// What to do when there is no terminal. Defaults to the plain spawn, which
    /// is what gg did before this existed.
    /// </param>
    /// <param name="bar">
    /// The top row. It says what ends the session, because a person looking at
    /// somebody else's editor cannot ask gg what it is waiting for.
    /// </param>
    /// <param name="host">
    /// How to run the child. Defaults to <see cref="PtyHost.RunAsync"/>; a test
    /// passes one that cannot start, because a machine missing the native
    /// library is a case that has to be handled and cannot be arranged.
    /// </param>
    /// <param name="say">
    /// Where a word to the person goes when this machine cannot host. Defaults
    /// to the console, which is free at the moment it is used — the editor has
    /// not taken the screen yet, and once it has, nothing gg writes will be read.
    /// </param>
    /// <param name="notesIn">
    /// Where the file handed to the editor is put. Defaults to the temp
    /// directory, which is where gg has always put it.
    /// <para>
    /// <b>Named because a test has to be able to watch it.</b> Asserting that
    /// the draft was deleted means looking at a directory, and the shared temp
    /// directory is one every other test writing a draft is also using - a count
    /// taken there is a count of somebody else's work as much as this one's.
    /// </para>
    /// </param>
    public PtyEditorSession(
        string? editorCommand = null,
        Func<IHostTerminal?>? terminal = null,
        IEditorSession? unhosted = null,
        string bar = "gg · editing — save and quit to come back",
        string? notesIn = null,
        HostRun? host = null,
        Action<string>? say = null)
    {
        _editorCommand = editorCommand
            ?? Environment.GetEnvironmentVariable("EDITOR")
            ?? "vi";
        _terminal = terminal ?? OwnedTerminal.Open;
        _unhosted = unhosted ?? new EditorSession(_editorCommand);
        _bar = bar;
        _notesIn = notesIn ?? Path.GetTempPath();
        _host = host ?? PtyHost.RunAsync;
        _say = say ?? System.Console.WriteLine;
    }

    public string Edit(string initialText)
    {
        var terminal = _terminal();
        if (terminal is null)
        {
            return _unhosted.Edit(initialText);
        }

        // THE TERMINAL IS CLAIMED, so from here every path closes it. It is a
        // held resource rather than a value - an open /dev/tty and a signal
        // registration - and writing the draft one line above this block left
        // both behind whenever the disk was full or the directory had gone.
        try
        {
            var file = Path.Combine(_notesIn, $"gg-notes-{Guid.NewGuid():N}.md");
            File.WriteAllText(file, initialText);

            var parts = _editorCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            try
            {
                _host(
                    terminal,
                    parts[0],
                    [.. parts.Skip(1), file],
                    Directory.GetCurrentDirectory(),
                    // NOTHING CHANGES WHILE AN EDITOR IS UP. Saving and quitting
                    // is what ends this, and the editor says so itself - so one
                    // row is the whole of what gg has to add.
                    _ => (string[])[_bar],
                    // AND GG TAKES NO KEY AT ALL HERE. An editor session has
                    // nothing gg could show that the editor is not already
                    // showing, and a key charged for a panel that never opens is
                    // a key taken from vim for nothing.
                    _ => false,
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception missing) when (
                missing is DllNotFoundException or EntryPointNotFoundException)
            {
                // THE NATIVE LIBRARY IS NOT HERE, so this machine cannot host.
                // It still has an editor - it has had one all along - and the
                // bar is a nicety while the editor is not.
                //
                // NAMED RATHER THAN BARE, and that is the whole care in this
                // block. Falling back on any failure would turn "your $EDITOR is
                // not installed" into a silent second attempt at the same thing,
                // and leave a person with an editor that never opens and no
                // reason given. Only this is a reason to stop hosting.
                //
                // AND SAID OUT LOUD, because the release tarball carries the
                // binary and nothing else: on a machine installed the documented
                // way this is not the rare case, it is every session. Silence
                // there is indistinguishable from a feature nobody built, and a
                // missing file is something a person can actually go and get.
                _say("gg could not open its own terminal view: libporta_pty is not "
                   + "installed beside gg. Your editor opens as normal, without the gg bar.");

                return _unhosted.Edit(initialText);
            }

            try
            {
                // THE FILE, NOT THE EXIT CODE. An editor that was abandoned, or
                // killed, still leaves whatever was written before that - and
                // every editor a person might set here disagrees about what its
                // exit code means. What is on disk is the one answer all of them
                // give.
                return File.ReadAllText(file);
            }
            finally
            {
                // HOWEVER IT ENDED. This file holds whatever a person was
                // writing, in a directory everybody on this machine can read.
                File.Delete(file);
            }
        }
        finally
        {
            (terminal as IDisposable)?.Dispose();
        }
    }
}
