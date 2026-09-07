using System.Text;
using Porta.Pty;
using XTerm.Options;

// `Terminal` is XTerm.NET's emulator type and also the root namespace of
// Terminal.Gui, which this assembly references. Named once here rather than
// qualified at every use below.
using XTermTerminal = XTerm.Terminal;

namespace Gg.Console;

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
    /// <b>The bar costs the child a row.</b> The child is told the screen is
    /// <see cref="IHostTerminal.Rows"/> minus one, which is what keeps the top
    /// row gg's: a full-screen program cannot paint a row it does not believe
    /// exists.
    /// </remarks>
    public static async Task<int> RunAsync(
        IHostTerminal terminal,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string bar,
        CancellationToken cancellationToken)
    {
        var columns = Math.Max(terminal.Columns, 20);
        var rows = Math.Max(terminal.Rows - 1, 5);

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

            // THE BAR GOES UP BEFORE THE CHILD SAYS ANYTHING. Painting only on
            // arrival ties gg's own row to the child having written something,
            // and an editor that opens on an empty file writes nothing at all -
            // so the one row gg kept stayed blank for as long as the person sat
            // there. Found by an editor test, fixed here, because the bar is the
            // host's promise and not the caller's.
            terminal.Paint(PtyScreen.Paint(emulator, rows, columns, bar));

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

                emulator.Write(Encoding.UTF8.GetString(buffer, 0, read));
                terminal.Paint(PtyScreen.Paint(emulator, rows, columns, bar));
            }

            pty.WaitForExit(Timeout.Infinite);

            await stopping.CancelAsync();
            await typing;

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
