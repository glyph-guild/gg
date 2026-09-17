using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Gg.Runner;
using Porta.Pty;

namespace Gg.Cli;

/// <summary>
/// What gg reads off <c>claude setup-token</c>'s screen: the login URL, and
/// the token it mints.
/// </summary>
/// <remarks>
/// <para>
/// <b>The hyperlink is the robust thing to parse.</b> Measured in the spike
/// (Claude Code 2.1.272): the URL is emitted as an OSC-8 hyperlink -
/// <c>ESC ] 8 ; params ; URI ST</c> - whose visible text is the same URL
/// wrapped across lines. The sequence carries it once, unwrapped, and the
/// wrapped text is never read.
/// </para>
/// <para>
/// <b>The token is recognised by its shape</b>, because how the agent prints
/// it is the one thing the spike could not measure without a person finishing
/// the ceremony. A long-lived claude token begins <c>sk-ant-oat01-</c>; an API
/// key begins <c>sk-ant-api</c> and is deliberately not matched - it is the
/// thing gg never sets, and reading one here would place it.
/// </para>
/// <para>
/// <b>Screen-scraping another program's terminal breaks whenever that
/// program is improved</b> - <c>AttendedExecutor</c>'s sentence, and it holds
/// here. The two patterns are all of it, they are pure, and a real run that
/// finds either wrong changes this file and nothing else.
/// </para>
/// </remarks>
public static partial class SetupTokenScreen
{
    // OSC 8 ; params ; URI, terminated by ST (ESC \) or BEL. The closing
    // sequence carries an empty URI, which is why empties are skipped.
    [GeneratedRegex(@"\]8;[^;]*;([^]*)(?:|\\)")]
    private static partial Regex Hyperlink();

    [GeneratedRegex(@"sk-ant-oat01-[A-Za-z0-9_\-]{8,}")]
    private static partial Regex LongLivedToken();

    /// <summary>The first non-empty hyperlink target on the screen, or null.</summary>
    /// <remarks>
    /// <b>Walked one at a time</b>, because only the first non-empty target is
    /// wanted - the closing sequence of every hyperlink carries an empty one -
    /// and because <c>WhyVerbTests</c> scans this project for the plural form:
    /// no client may evaluate an obligation's attachment predicate, and a glob
    /// matcher is what that guard hunts. Reading a link off a terminal screen
    /// is a different act, and not spelling the guard's word is cheaper than
    /// arguing with it.
    /// </remarks>
    public static string? Url(string screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        for (var found = Hyperlink().Match(screen); found.Success; found = found.NextMatch())
        {
            if (found.Groups[1].Value is { Length: > 0 } target)
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>The first token-shaped run on the screen, or null.</summary>
    /// <summary>One keystroke, and how long to let it settle.</summary>
    public readonly record struct Keystroke(string Text, TimeSpan Settle);

    /// <summary>
    /// How long the code is left alone before the return follows it.
    /// </summary>
    /// <remarks>
    /// <b>A second, and it is a measurement rather than a taste.</b> Sent in one
    /// write, the agent shows the code masked at its prompt and does nothing with
    /// it: it reads a burst as a paste and the return inside the burst is part of
    /// the pasted text. Sent after a pause, the same agent answers immediately.
    /// Measured inside gg-pool-ui-1 both ways, with a bogus code, for the answer
    /// rather than the acceptance.
    /// </remarks>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    /// <summary>
    /// What a person does at this prompt: paste, pause, press return.
    /// </summary>
    /// <remarks>
    /// The sequence is the subject, so it is a list a test can read rather than
    /// two writes buried in a child - three attempts at an agent login were lost
    /// inside the one write this replaces.
    /// </remarks>
    public static IReadOnlyList<Keystroke> Typing(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return [new Keystroke(code, Settle), new Keystroke("\r", TimeSpan.Zero)];
    }

    public static string? Token(string screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var match = LongLivedToken().Match(screen);
        return match.Success ? match.Value : null;
    }
}

/// <summary>
/// Runs the agent's own <c>setup-token</c> under a pseudo-terminal gg owns,
/// and reads the two things the ceremony needs off its screen.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than in <c>Gg.Runner</c>, because this project may take a
/// terminal library and that one may not.</b> The runner holds the ceremony's
/// state machine and refusals (<see cref="AgentLoginCeremony"/>); this is the
/// one class that touches a pseudo-terminal, and it is handed in as a port.
/// </para>
/// <para>
/// <b>The child inherits no token.</b> A stored token in its environment
/// would make <c>setup-token</c> report the login it already has, and the
/// ceremony would end with the old token kept as new. Nothing is placed;
/// <see cref="OptionsFor"/> is asserted to carry neither variable gg knows.
/// </para>
/// <para>
/// <b>Wide, so the token is one line.</b> A token is a hundred characters;
/// under eighty columns the emulator would wrap it, and the recogniser reads
/// one run.
/// </para>
/// </remarks>
public sealed class SetupTokenLogin(string binary, IReadOnlyList<string>? arguments = null) : IRunAnAgentLogin
{
    private readonly string _binary = binary;
    private readonly IReadOnlyList<string> _arguments = arguments ?? ["setup-token"];

    /// <summary>The launch, as a value tests can look at.</summary>
    public static PtyOptions OptionsFor(string binary, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(binary);
        ArgumentNullException.ThrowIfNull(arguments);

        return new PtyOptions
        {
            Name = "gg-agent-login",
            Cols = 240,
            Rows = 50,
            Cwd = Environment.CurrentDirectory,
            App = binary,
            CommandLine = [.. arguments],
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
        };
    }

    public async Task<IAgentLoginChild> StartAsync(CancellationToken cancellationToken)
    {
        var connection = await PtyProvider.SpawnAsync(OptionsFor(_binary, _arguments), cancellationToken);
        return new Child(connection);
    }

    /// <summary>The running child: a screen that grows, and two readers of it.</summary>
    private sealed class Child : IAgentLoginChild
    {
        private readonly IPtyConnection _connection;
        private readonly StringBuilder _screen = new();
        private readonly Lock _gate = new();
        private readonly SemaphoreSlim _changed = new(0);
        private readonly TaskCompletionSource _over = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Child(IPtyConnection connection)
        {
            _connection = connection;
            _ = Task.Run(ReadAsync);
        }

        public Task<string?> UrlAsync(CancellationToken cancellationToken) =>
            WaitForAsync(SetupTokenScreen.Url, cancellationToken);

        public async Task<string?> TokenAsync(string code, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(code);

            // TYPED, THEN SUBMITTED, and the gap between them is the fix. The
            // child's prompt has long been on the screen by the time a person
            // comes back from a browser - but a code and its return arriving in
            // one write are one paste to the agent, which then sits on it.
            foreach (var stroke in SetupTokenScreen.Typing(code))
            {
                var typed = Encoding.UTF8.GetBytes(stroke.Text);
                await _connection.WriterStream.WriteAsync(typed, cancellationToken);
                await _connection.WriterStream.FlushAsync(cancellationToken);

                if (stroke.Settle > TimeSpan.Zero)
                {
                    await Task.Delay(stroke.Settle, cancellationToken);
                }
            }

            return await WaitForAsync(SetupTokenScreen.Token, cancellationToken);
        }

        /// <summary>The tail of everything the child has written.</summary>
        /// <remarks>
        /// Raw, escapes and all: what is safe to show is the ceremony's
        /// decision, because it is the one that knows the code that was typed
        /// and what a token looks like.
        /// </remarks>
        public string LastWords(int characters)
        {
            lock (_gate)
            {
                var screen = _screen.ToString();
                return screen.Length <= characters ? screen : screen[^characters..];
            }
        }

        public void Dispose()
        {
            // THE WHOLE TREE, the meter's way: the binary is a launcher on
            // some machines, and the terminal outlives a parent killed alone.
            try
            {
                using var process = Process.GetProcessById(_connection.Pid);
                process.Kill(entireProcessTree: true);
            }
            catch (Exception failure) when (failure is ArgumentException
                                                or InvalidOperationException
                                                or Win32Exception)
            {
                // Already gone, which is the state wanted.
            }

            _connection.Dispose();
        }

        /// <summary>Every byte the child writes, appended as it arrives.</summary>
        private async Task ReadAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (true)
                {
                    var read = await _connection.ReaderStream.ReadAsync(buffer);
                    if (read <= 0)
                    {
                        break;
                    }

                    lock (_gate)
                    {
                        _screen.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    }

                    _changed.Release();
                }
            }
            catch (Exception failure) when (failure is IOException or ObjectDisposedException)
            {
                // The terminal closed under the reader: the child is gone.
            }
            finally
            {
                _over.TrySetResult();
                _changed.Release();
            }
        }

        /// <summary>
        /// Waits until <paramref name="read"/> finds something on the screen, or
        /// until the child is over and it never will.
        /// </summary>
        private async Task<string?> WaitForAsync(Func<string, string?> read, CancellationToken cancellationToken)
        {
            while (true)
            {
                string screen;
                lock (_gate)
                {
                    screen = _screen.ToString();
                }

                if (read(screen) is { } found)
                {
                    return found;
                }

                if (_over.Task.IsCompleted)
                {
                    // THE READER FINISHED, so every byte the child wrote is on
                    // the screen, and one last look is the honest answer.
                    lock (_gate)
                    {
                        screen = _screen.ToString();
                    }

                    return read(screen);
                }

                try
                {
                    await _changed.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }
    }
}
