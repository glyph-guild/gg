using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A child gg hosts in a pseudo terminal ends the host's read when it exits, on Windows too.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by the Windows console probe.</b> The editor ran in the host, wrote its file and
/// exited, and gg sat on its bar for ever. <see cref="PtyHost"/> reads the child's output to
/// the end before it asks for the exit code. On Windows a pseudo console keeps that output
/// open after its child exits, until the pseudo console itself is closed, so the end never
/// came.
/// </para>
/// <para>
/// <b>Porta.Pty closes it on exit when asked for asynchronous IO</b>, and only then. So every
/// pseudo terminal the console starts asks for it on Windows, through one builder, and a scan
/// keeps a second construction site from appearing without it.
/// </para>
/// </remarks>
public class AHostedChildEndsWhenItExitsTests
{
    [Test]
    public async Task On_Windows_the_pseudo_console_closes_when_the_child_exits()
    {
        var options = ChildPty.Options(80, 24, "C:\\work", "editor.exe", ["notes.md"], windows: true);

        await Assert.That(options.UseAsyncIo).IsTrue()
            .Because("without it the output stays open after the child exits and the host waits for ever.");
    }

    [Test]
    public async Task Elsewhere_the_read_ends_on_its_own_and_nothing_changes()
    {
        // ON LINUX ASYNCHRONOUS IO NEEDS pidfd_open, kernel 5.3 or newer, and a pty that ends
        // its read when the child exits already does what is wanted.
        var options = ChildPty.Options(80, 24, "/work", "vi", ["notes.md"], windows: false);

        await Assert.That(options.UseAsyncIo).IsFalse();
    }

    [Test]
    public async Task The_rest_of_the_options_are_what_was_asked_for()
    {
        var options = ChildPty.Options(120, 40, "/work", "vi", ["-n", "notes.md"], windows: false);

        await Assert.That(options.Cols).IsEqualTo(120);
        await Assert.That(options.Rows).IsEqualTo(40);
        await Assert.That(options.Cwd).IsEqualTo("/work");
        await Assert.That(options.App).IsEqualTo("vi");
        await Assert.That(options.CommandLine).IsEquivalentTo(["-n", "notes.md"]);
        await Assert.That(options.Environment["TERM"]).IsEqualTo("xterm-256color");
    }

    [Test]
    public async Task Every_pseudo_terminal_the_console_starts_is_built_by_the_one_builder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var console = Path.Combine(
            dir?.FullName ?? throw new InvalidOperationException("Gg.sln not found"), "Gg.Console");

        var elsewhere = Directory.EnumerateFiles(console, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.EndsWith($"{Path.DirectorySeparatorChar}ChildPty.cs", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("new PtyOptions", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        await Assert.That(elsewhere).IsEmpty()
            .Because("a pseudo terminal built anywhere else would not end when its child exits on Windows. "
                   + "Found: " + string.Join(", ", elsewhere));
    }
}
