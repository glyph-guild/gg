using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A read a key asks for is started, and served by the reader the root wires.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT, AND IT MEANS THE AIRSPACE TAB HAS NEVER SHOWN A
/// DOCUMENT.</b> <c>ConsoleEstate.Read</c> is the only thing that fills
/// <c>Estate.Names</c> and <c>Estate.Working</c>. It is called from one place —
/// the <c>envelope</c> port — which is called from one place, the
/// <c>ToggleEnvelope</c> arm of <c>ConsoleLoop</c>. That arm is unreachable:
/// <c>ToggleEnvelope</c> is in <c>ShellCommands.Reads</c>, and
/// <c>ExitCommand</c> is set only for <c>Handled</c> commands, so the loop
/// never sees it.
/// </para>
/// <para>
/// <b>And the path that replaced it does not fire on the key.</b>
/// <c>_reads.Start</c> is called from <c>OnTabChanged</c> and from nowhere
/// else. <c>Dispatch</c> — which is what a keystroke and a modal button both
/// go through — has no such block, and <c>Render</c> assigns the tab bar's
/// value under <c>_syncing</c>, so a keypress never arrives at
/// <c>OnTabChanged</c> either. Pressing <c>e</c> reduces, renders, and asks
/// nobody anything.
/// </para>
/// <para>
/// <b>Then the reader ignores which command asked.</b> The root wires
/// <c>(_, current) =&gt; ConsoleFlightLog.Patch(data, current)</c> — one read for
/// three commands, and it short-circuits unless a flight is open. So even the
/// click that does reach <c>OnTabChanged</c> fetches the wrong thing.
/// </para>
/// <para>
/// <b>Three breaks on one path, and every test around them green</b>, because
/// <c>TheEstateReachesTheConsoleTests</c> passes <c>EstateOnThisMachine</c> in
/// by hand. It is the fourth instance in a day of one shape: a fixture
/// supplying what the composition root does not. The ratchet is the one from
/// <c>gg:TheToolServerIsHandedWhatItNeedsTests</c> — hold both ends of the
/// wire, because either alone is silent.
/// </para>
/// </remarks>
public class EveryReadAKeyAsksForIsServedTests
{
    private static string Read(params string[] parts)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        return File.ReadAllText(Path.Combine(
            [(at ?? throw new InvalidOperationException("Gg.sln not found")).FullName, .. parts]));
    }

    [Test]
    public async Task A_key_that_wants_a_read_starts_one()
    {
        // DISPATCH IS WHERE A KEY AND A BUTTON BOTH ARRIVE, and it is the one
        // that does not ask. Asserted over the source because Dispatch is
        // private and ConsoleScreen cannot be constructed without a terminal -
        // which is the reason CollectionViews and ConsoleTheme are factories
        // and stated in both.
        var screen = Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var at = screen.IndexOf("private void Dispatch(", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1)
            .Because("Dispatch was renamed, so this scan reads nothing.");

        var body = screen[at..screen.IndexOf("\n    private", at + 1, StringComparison.Ordinal)];

        // ASSERTED THROUGH THE METHOD RATHER THAN THE BLOCK. This first
        // demanded `ShellCommands.Reads` inline in Dispatch, which would have
        // been satisfied by a SECOND copy of the read-starting block - and two
        // copies is how one of the two callers came to be missing it in the
        // first place. What matters is that Dispatch starts a read; where the
        // Reads check lives is the other assertion below.
        await Assert.That(body).Contains("Asked(command)", StringComparison.Ordinal)
            .Because("a command in Reads that arrives by key is a read nobody starts. The "
                   + "estate has never reached the airspace tab for this reason, and "
                   + "pressing the key that is supposed to fetch it does nothing at all. "
                   + "Dispatch:\n" + body);

        var asked = screen.IndexOf("private void Asked(", StringComparison.Ordinal);

        await Assert.That(asked).IsGreaterThan(-1)
            .Because("Dispatch calls something this scan cannot find.");

        await Assert.That(screen[asked..screen.IndexOf(
                "\n    private", asked + 1, StringComparison.Ordinal)])
            .Contains("ShellCommands.Reads", StringComparison.Ordinal)
            .Because("and it is the declaration that decides which commands want one, "
                   + "rather than a list written here.");
    }

    [Test]
    public async Task The_reader_the_root_wires_answers_the_command_it_is_given()
    {
        // ONE READ FOR THREE COMMANDS. The port takes a Command and the wired
        // lambda discards it, so ToggleEnvelope and ToggleRepositories both
        // fetch a flight's story - which short-circuits when no flight is
        // open, so they fetch nothing.
        var root = Read("Gg.Cli", "Program.cs");

        var at = root.IndexOf("new Gg.Console.BackgroundReads(", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1)
            .Because("the reader is wired somewhere else now, so this scan reads nothing.");

        var wired = root[at..root.IndexOf("))", at, StringComparison.Ordinal)];

        await Assert.That(wired).DoesNotContain("(_, current)", StringComparison.Ordinal)
            .Because("a discarded Command is three commands served one read. The port "
                   + "carries which one asked precisely so this can answer it. Wired: "
                   + wired);
    }

    [Test]
    public async Task Every_command_in_reads_is_named_by_the_reader()
    {
        // THE RATCHET RATHER THAN THE FIX. A fourth command added to Reads
        // with no arm in the reader is the same defect again, and it is silent
        // in the same direction: the key works, the pane stays empty, and the
        // test that renders the pane passes its own state in.
        var root = Read("Gg.Cli", "Program.cs");

        var missing = ShellCommands.Reads
            .Where(command => !root.Contains($"Command.{command}", StringComparison.Ordinal))
            .Select(command => command.ToString())
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("a command the console asks a read for, that the root's reader does "
                   + "not name, is a keypress that fetches somebody else's answer or "
                   + "nothing. Found: " + string.Join(", ", missing));
    }
}
