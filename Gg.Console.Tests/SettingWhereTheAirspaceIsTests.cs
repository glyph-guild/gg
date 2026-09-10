using System.Text.RegularExpressions;
using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Where the airspace is, said at the top of the tab and settable from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The setting existed and the console could only read it.</b>
/// <c>GG_AIRSPACE</c> and the <c>airspace</c> key have named the working copy
/// since the verbs stopped taking the current directory — but a person sitting
/// in the console had no way to say where theirs was, so the answer to "not
/// configured" was to leave, edit a JSON file, and come back.
/// </para>
/// <para>
/// <b>Which is why the fallback had to go with it.</b> The read, pull and apply
/// delegates resolved through the same helper the command line uses, which falls
/// back to the process's current directory. For a verb somebody typed in a tree
/// they chose that is right; for a console launched from wherever, it meant `p`
/// wrote an <c>airspace/</c> tree into the launch directory — and because that
/// directory is usually not a git tree, <c>Git.Status</c> answered empty, the
/// dirty-tree refusal could not fire, and pull simply wrote. The doctor warns
/// about exactly that state; the surface that most needed the guard did not have
/// it.
/// </para>
/// <para>
/// <b>Refusing was only ever half an answer.</b> A pane that says "not
/// configured" and offers nothing is a dead end, which is presumably why the
/// fallback was there. The pair is what works: the tab says where it is, and the
/// same tab is where you say it.
/// </para>
/// </remarks>
public class SettingWhereTheAirspaceIsTests
{
    private static EstateOnThisMachine At(string? root) => new()
    {
        Root = root,
        IsRepository = root is not null,
        Names = new Gg.Contracts.EnvelopeTopology { Names = [] },
    };

    [Test]
    public async Task The_key_asks_where_while_the_airspace_tab_is_showing()
    {
        await Assert.That(Keymap.Resolve(
                KeyStroke.Char('w'), new KeymapContext(UiMode.Normal, TabId.Envelope)))
            .IsEqualTo(Command.SetAirspacePath);
    }

    [Test]
    public async Task It_does_nothing_on_any_other_tab()
    {
        foreach (var tab in Tabs.All.Where(t => t != TabId.Envelope))
        {
            await Assert.That(Keymap.Resolve(
                    KeyStroke.Char('w'), new KeymapContext(UiMode.Normal, tab)))
                .IsNull()
                .Because($"`w' is the airspace tab's, and it resolved on {Tabs.Name(tab)}.");
        }
    }

    [Test]
    public async Task Saying_where_is_the_shell_s_work()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.SetAirspacePath)
            .Because("it takes text, and nothing in this console is written by typing into a "
                   + "widget - the terminal goes to $EDITOR and a file is written after.");

        var before = new AppState { ActiveTab = TabId.Envelope };
        await Assert.That(Reducer.Reduce(before, Command.SetAirspacePath)).IsEqualTo(before);
    }

    [Test]
    public async Task The_top_of_the_tab_says_where_the_airspace_is()
    {
        var text = PaneText.Estate(new AppState { Estate = At("/home/someone/airspace") });

        await Assert.That(text.Split('\n')[0])
            .Contains("/home/someone/airspace", StringComparison.Ordinal)
            .Because("the first line is where somebody looks to find out which tree they are "
                   + "about to write, and it is the question the doctor exists to answer for "
                   + "the command line.");
    }

    [Test]
    public async Task An_unset_airspace_says_so_and_names_the_key()
    {
        var text = PaneText.Estate(new AppState { Estate = At(null) });

        await Assert.That(text).Contains("w", StringComparison.Ordinal);
        await Assert.That(text.Split('\n')[0]).Contains("not set", StringComparison.OrdinalIgnoreCase)
            .Because("a pane that reports a state and no way out of it is a dead end, and "
                   + "this one has a key one line away.");
    }

    [Test]
    public async Task The_console_never_falls_back_to_the_directory_it_was_launched_from()
    {
        // THE DEFECT THIS PAIR CLOSES, asserted at the source because there is
        // no behaviour to observe: EstateRoot() answers a path either way, so a
        // console using it looks identical to one that was configured - right
        // up to the moment `p` writes a tree into somebody's home directory.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gg.sln")))
        {
            root = root.Parent;
        }

        var source = File.ReadAllText(
            Path.Combine(root!.FullName, "Gg.Cli", "Program.cs"));

        // The console's four delegates, by the argument each is handed.
        var console = source[source.IndexOf("new ConsoleLoop(", StringComparison.Ordinal)..];

        await Assert.That(Regex.Matches(console, @"EstateRoot\(\)").Count).IsEqualTo(0)
            .Because("the command line keeps that fallback, because a verb was typed in a "
                   + "directory somebody chose. A console was launched from one they did "
                   + "not, so an unset airspace has to read as unset.");
    }

    [Test]
    public async Task Where_it_is_lands_in_the_configuration_file()
    {
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-where-{Guid.NewGuid():N}", "config.json");

        var said = ConsoleAirspacePath.Set(at, current: null, ask: _ => "/tmp/my-airspace");

        try
        {
            var read = ConfigurationFile.Read(at);

            await Assert.That(read.Configuration?.Airspace).IsEqualTo("/tmp/my-airspace")
                .Because("the file is the durable answer, and the same one the verbs and the "
                       + "doctor resolve through - a value the console held privately would "
                       + "be a second place this lives.");

            await Assert.That(said).Contains("/tmp/my-airspace", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(at)!, recursive: true);
        }
    }

    [Test]
    public async Task The_editor_is_handed_what_is_there_now()
    {
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-where-{Guid.NewGuid():N}", "config.json");

        var seen = "";
        try
        {
            _ = ConsoleAirspacePath.Set(at, current: "/tmp/already-here", ask: text =>
            {
                seen = text;
                return text;
            });

            await Assert.That(seen).IsEqualTo("/tmp/already-here")
                .Because("changing a path means editing it, and an empty buffer asks somebody "
                       + "to retype a directory they already have.");
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(at)!))
            {
                Directory.Delete(Path.GetDirectoryName(at)!, recursive: true);
            }
        }
    }

    [Test]
    public async Task Nothing_typed_changes_nothing()
    {
        // THE ESCAPE. Somebody who opened the editor and thought better of it
        // has to be able to leave without clearing the value they had - and
        // writing an empty path would make every airspace verb refuse.
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-where-{Guid.NewGuid():N}", "config.json");

        var said = ConsoleAirspacePath.Set(at, current: "/tmp/already-here", ask: _ => "  \n");

        await Assert.That(File.Exists(at)).IsFalse()
            .Because("nothing was decided, so nothing is written - not even a file holding "
                   + "the value that was already in force.");
        await Assert.That(said).Contains("unchanged", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task A_path_that_is_not_there_yet_is_accepted_and_said_so()
    {
        // ACCEPTED, because the order a person works in is set-then-clone, or
        // set-then-pull: AirspaceTree.Write creates the tree it renders into. A
        // refusal here would make the setting impossible to use before the
        // directory exists, which is every first time.
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-where-{Guid.NewGuid():N}", "config.json");

        var missing = Path.Combine(Path.GetTempPath(), $"gg-not-yet-{Guid.NewGuid():N}");
        var said = ConsoleAirspacePath.Set(at, current: null, ask: _ => missing);

        try
        {
            await Assert.That(ConfigurationFile.Read(at).Configuration?.Airspace)
                .IsEqualTo(missing);
            await Assert.That(said).Contains("not there", StringComparison.OrdinalIgnoreCase)
                .Because("it is a fact worth one clause, because the next thing a person does "
                       + "is press `p' and it will be created under them.");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(at)!, recursive: true);
        }
    }
}
