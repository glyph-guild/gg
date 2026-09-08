using System.Text;
using Porta.Pty;
using XTerm.Options;

// `Terminal` is XTerm.NET's emulator type and also the root namespace of
// Terminal.Gui, which this assembly references. Named once here rather than
// qualified at every use below.
using XTermTerminal = XTerm.Terminal;

namespace Gg.Console;

/// <summary>Running a child in a hosted terminal, as something injectable.</summary>
/// <remarks>
/// <b>A seam, and a narrow one.</b> It exists so a caller can be tested against a
/// host that cannot start — which is not hypothetical: Porta.Pty P/Invokes a
/// native library that ships beside gg rather than inside it, and .NET resolves a
/// P/Invoke on first call, so a gg installed without that file builds, starts and
/// reports its version before failing on the key a person pressed. Nothing but a
/// test is expected to pass anything other than <see cref="PtyHost.RunAsync"/>.
/// </remarks>
public delegate Task<int> HostRun(
    IHostTerminal terminal,
    string command,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    Func<IReadOnlyList<string>> panel,
    CancellationToken cancellationToken);

/// <summary>
/// Runs a child in a pseudo-terminal gg owns, with a gg bar on the top row, and
/// gives the terminal back when it ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>One implementation, every caller.</b> The editor handoff and the agent
/// handoff want the same thing, and every terminal defect found here was subtle
/// enough that a second copy would carry its own version of it. The bar text is
/// the only difference between them.
/// </para>
/// <para>
/// <b>It does not break terminal release.</b> Callers run between UI sessions,
/// with Terminal.Gui torn down and the terminal provably free — the slot the
/// editor spawn already occupied. What changes is that gg MEDIATES the child
/// instead of handing it the raw terminal, so every byte in and out passes
/// through this process. Nothing is stored: this type has no fields, and a test
/// asserts that, because a field here would hand the next caller the last
/// child's screen.
/// </para>
/// <para>
/// <b>And nothing is written to disk.</b> A spike traced every byte in and out
/// to a temp file, which is how the defects below were found and which also put
/// a person's editor buffer and agent transcript on disk unredacted. A test
/// forbids it coming back with the code it was used on.
/// </para>
/// </remarks>
public static class PtyHost
{
    /// <summary>The escape byte, written as an escape.</summary>
    /// <remarks>
    /// <b>NEVER a literal control character in the source.</b> See
    /// <see cref="PtyScreen"/>: as a raw byte it was stripped to an empty string
    /// and every sequence went out as visible text.
    /// </remarks>
    private const string Esc = "\u001b";

    /// <summary>
    /// Hosts <paramref name="command"/> until it ends, and answers with its exit
    /// code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bar costs the child a row.</b> The child is told the screen is
    /// <see cref="IHostTerminal.Rows"/> minus one, which is what keeps the top
    /// row gg's: a full-screen program cannot paint a row it does not believe
    /// exists.
    /// </para>
    /// <para>
    /// <b>And it is asked for on every frame rather than given once.</b> What
    /// gg's rows have to say changes while the session runs — most of all once a
    /// composing agent has submitted, because a person who cannot tell whether
    /// gg received anything will submit again. Something handed over at the
    /// start can only say what was true then.
    /// </para>
    /// <para>
    /// <b>And how MANY rows changes too.</b> One while gg is only saying what
    /// ends the session; several once somebody has asked to see the envelope. So
    /// the child's size depends on the panel as well as on the terminal, and a
    /// single <c>Repaint</c> owns both — separate paths for them would be two
    /// places to get the same arithmetic right.
    /// </para>
    /// </remarks>
    public static async Task<int> RunAsync(
        IHostTerminal terminal,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        Func<IReadOnlyList<string>> panel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var (columns, rows) = Fit(terminal, panel().Count);

        var emulator = new XTermTerminal(new TerminalOptions { Cols = columns, Rows = rows });

        var options = new PtyOptions
        {
            Name = "gg",
            Cols = columns,
            Rows = rows,
            Cwd = workingDirectory,
            App = command,
            CommandLine = [.. arguments],
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
        };

        // MODES THE CHILD DID NOT ASK FOR, TURNED OFF FIRST. Whatever had the
        // terminal before gg may have left mouse reporting, focus events or
        // bracketed paste on, and those arrive as escape sequences the child
        // never enabled and cannot interpret.
        terminal.Paint($"{Esc}[?1000l{Esc}[?1002l{Esc}[?1003l{Esc}[?1004l{Esc}[?2004l");

        // The alternate screen, so the scrollback a person had before gg started
        // is still there after it ends.
        terminal.Paint($"{Esc}[?1049h{Esc}[2J");

        // RAW MODE AFTER THE FIRST PAINT, and the ordering is load-bearing: .NET
        // configures the terminal's termios when the console is first used, so
        // raw mode applied before that is silently wiped and every read then
        // blocks until Enter.
        var cooked = RawMode.Enter(terminal.Descriptor);

        try
        {
            using var pty = await PtyProvider.SpawnAsync(options, cancellationToken);
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var typing = Forward(terminal, pty, stopping.Token);

            // ONE THREAD IN THE EMULATOR AT A TIME. Two get here: this loop,
            // writing what the child produced, and the SIGWINCH handler, resizing
            // it. XTerm.NET makes no thread-safety promise and neither does a
            // screen buffer being reallocated under a write - and the second
            // hazard is gg's own, because two frames painted at once arrive
            // spliced together on the terminal.
            //
            // Found by reading rather than by a failure. A test for it would be
            // a stress loop that passes on most runs while the defect is present,
            // which is worse than no test: it would be cited as evidence.
            var screen = new Lock();

            // WHAT A PERSON DRAGGING THE CORNER OF THEIR WINDOW CAUSES. Both
            // sides have to move: the pty, so every program inside it is told,
            // and the emulator, so gg paints the shape the screen now is.
            // Resizing only the emulator looks right and is wrong to the child;
            // resizing only the pty is the reverse.
            //
            // A LOCAL, NOT A FIELD. This host holds no state between calls and a
            // test asserts it does not, so the subscription lives exactly as
            // long as the session and is taken off again below.
            void Repaint()
            {
                var kept = panel();
                var (width, height) = Fit(terminal, kept.Count);

                lock (screen)
                {
                    if (width != columns || height != rows)
                    {
                        try
                        {
                            pty.Resize(width, height);
                        }
                        catch (IOException)
                        {
                            // The child is already gone. A resize arriving in
                            // the gap between its exit and this loop noticing is
                            // ordinary, not an error, and must not take the
                            // session down on its way out.
                            return;
                        }

                        // Resize rather than assigning Cols and Rows: those
                        // setters are init-only, and this is the call that moves
                        // the buffer with them rather than leaving a screen that
                        // disagrees with itself.
                        emulator.Resize(width, height);

                        columns = width;
                        rows = height;
                    }

                    terminal.Paint(PtyScreen.Paint(emulator, rows, columns, kept));
                }
            }

            terminal.Resized += Repaint;

            // THE BAR GOES UP BEFORE THE CHILD SAYS ANYTHING. Painting only on
            // arrival ties gg's own row to the child having written something,
            // and an editor that opens on an empty file writes nothing at all -
            // so the one row gg kept stayed blank for as long as the person sat
            // there. Found by an editor test, fixed here, because the bar is the
            // host's promise and not the caller's.
            Repaint();

            // READ TO THE END BEFORE ASKING FOR THE EXIT CODE. The child can
            // write and exit faster than this loop runs, and a host that
            // cancelled the pump on exit would drop the last thing on the
            // screen - which for a short-lived child is everything it said.
            var buffer = new byte[8192];
            while (true)
            {
                int read;
                try
                {
                    read = await pty.ReaderStream.ReadAsync(buffer, cancellationToken);
                }
                catch (IOException)
                {
                    // The child ended and took its end of the pty with it. On
                    // Linux that is an EIO rather than a clean end of stream.
                    break;
                }

                if (read <= 0)
                {
                    break;
                }

                lock (screen)
                {
                    emulator.Write(Encoding.UTF8.GetString(buffer, 0, read));
                }

                // OUTSIDE THE WRITE'S LOCK, because Repaint takes it itself -
                // and it has to, since a resize can arrive between the two.
                Repaint();
            }

            pty.WaitForExit(Timeout.Infinite);

            await stopping.CancelAsync();
            await typing;

            terminal.Resized -= Repaint;

            return pty.ExitCode;
        }
        finally
        {
            // EVERY EXIT PATH, INCLUDING THE ONES THAT ARE NOT RETURNS. A child
            // that was killed, a spawn that threw and a cancellation all end
            // here, because a terminal left in raw mode is a person left with a
            // shell that does not echo.
            terminal.Paint($"{Esc}[?1049l");
            RawMode.Restore(terminal.Descriptor, cooked);
        }
    }

    /// <summary>
    /// How big the child's screen is: the terminal's, less gg's row.
    /// </summary>
    /// <remarks>
    /// <b>One place, called from one repaint.</b> gg's rows are subtracted at
    /// spawn, at every terminal resize, and every time the panel opens or
    /// closes; subtractions that drift apart are a screen wrong by however many
    /// rows until somebody resizes it back. The floors are for a terminal
    /// reporting nonsense - a resize can be observed mid-drag, and a pty of zero
    /// columns is not a thing a child can be told about.
    /// <para>
    /// <b>At least one row, always.</b> A caller handing over an empty panel
    /// still owes the child a screen it can compute, and gg keeping nothing is a
    /// state this host has no way to paint.
    /// </para>
    /// </remarks>
    private static (int Columns, int Rows) Fit(IHostTerminal terminal, int kept) =>
        (Math.Max(terminal.Columns, 20), Math.Max(terminal.Rows - Math.Max(kept, 1), 5));

    /// <summary>
    /// Everything the person types, into the child, until the child is gone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Synchronous reads on a thread of its own, polled.</b> A cancelled
    /// <c>ReadAsync</c> on a tty does not abort the syscall: the orphaned read
    /// stays pending and swallows the bytes that arrive next, so keystrokes
    /// reach nothing at all. What makes polling cheap rather than a spin is
    /// <see cref="RawMode"/> setting VMIN and VTIME to zero, so a read answers
    /// immediately with whatever is waiting and with nothing when there is
    /// nothing.
    /// </para>
    /// <para>
    /// <b>The buffer is drained first.</b> Whatever is already waiting was typed
    /// at gg, not at the child — the console prompts before it gets here, and
    /// text beginning with <c>a</c> or <c>i</c> would put an editor straight
    /// into insert mode.
    /// </para>
    /// </remarks>
    private static Task Forward(
        IHostTerminal terminal, IPtyConnection pty, CancellationToken stopping)
    {
        var keys = terminal.Keystrokes;
        var stale = new byte[1024];
        while (keys.Read(stale, 0, stale.Length) > 0)
        {
        }

        return Task.Factory.StartNew(
            () =>
            {
                var typed = new byte[1024];

                try
                {
                    while (!stopping.IsCancellationRequested)
                    {
                        var read = keys.Read(typed, 0, typed.Length);
                        if (read > 0)
                        {
                            pty.WriterStream.Write(typed, 0, read);
                            pty.WriterStream.Flush();
                            continue;
                        }

                        stopping.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(5));
                    }
                }
                catch (IOException)
                {
                    // The child's end of the pty is gone. There is nowhere left
                    // to forward to, which is not an error - it is the session
                    // ending.
                }
                catch (ObjectDisposedException)
                {
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }
}
