using System.Text.RegularExpressions;
using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Where the airspace is: what is written, and what is no longer guessed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The console used to fall back to the directory it was launched from.</b>
/// The read, pull and apply delegates resolved through the same helper the
/// command line uses, which answers the process's current directory when
/// nothing is configured. For a verb somebody typed in a tree they chose that
/// is right; for a console launched from wherever, it meant <c>p</c> wrote an
/// <c>airspace/</c> tree into the launch directory — and because that directory
/// is usually not a git tree, <c>Git.Status</c> answered empty, the dirty-tree
/// refusal could not fire, and pull simply wrote. The doctor warns about exactly
/// that state; the surface that most needed the guard did not have it.
/// </para>
/// <para>
/// <b>Refusing was only half an answer, which is why the field came with it.</b>
/// A pane that says "not set" and offers nothing is a dead end — presumably why
/// the fallback was there at all. The field at the bottom of the tab is the
/// other half, and it is
/// <see cref="TheAirspacePathIsTypedInTests"/>'s subject; this file is about the
/// value and where it lands.
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
    public async Task An_unset_airspace_says_so()
    {
        var text = PaneText.Estate(new AppState { Estate = At(null) });

        await Assert.That(text.Split('\n')[0])
            .Contains("not set", StringComparison.OrdinalIgnoreCase)
            .Because("an unset airspace is the state every key on this tab refuses in, so it "
                   + "is worth the first line rather than a silent path somebody did not "
                   + "choose.");
    }

    [Test]
    public async Task The_console_never_falls_back_to_the_directory_it_was_launched_from()
    {
        // ASSERTED AT THE SOURCE because there is no behaviour to observe:
        // EstateRoot() answers a path either way, so a console using it looks
        // identical to one that was configured - right up to the moment `p`
        // writes a tree into somebody's home directory.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gg.sln")))
        {
            root = root.Parent;
        }

        var source = File.ReadAllText(Path.Combine(root!.FullName, "Gg.Cli", "Program.cs"));
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

        try
        {
            var said = ConsoleAirspacePath.Set(at, "/tmp/my-airspace");

            await Assert.That(ConfigurationFile.Read(at).Configuration?.Airspace)
                .IsEqualTo("/tmp/my-airspace")
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
    public async Task A_path_that_is_not_there_yet_is_accepted_and_said_so()
    {
        // ACCEPTED, because the order a person works in is set-then-clone, or
        // set-then-pull: AirspaceTree.Write creates the tree it renders into. A
        // refusal here would make the setting impossible to use before the
        // directory exists, which is every first time.
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-where-{Guid.NewGuid():N}", "config.json");

        var missing = Path.Combine(Path.GetTempPath(), $"gg-not-yet-{Guid.NewGuid():N}");
        var said = ConsoleAirspacePath.Set(at, missing);

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

    [Test]
    public async Task A_broken_configuration_file_is_not_overwritten()
    {
        // ONE KEY'S WORTH OF SETTINGS REPLACING SOMEBODY'S DOCUMENT would take
        // the rest of it with them. The Environment page's editor is where a
        // broken file gets fixed, because that one hands over the bytes as they
        // are.
        var at = Path.Combine(
            Path.GetTempPath(), $"gg-broken-{Guid.NewGuid():N}", "config.json");

        Directory.CreateDirectory(Path.GetDirectoryName(at)!);
        await File.WriteAllTextAsync(at, "{ this is not json");

        try
        {
            var said = ConsoleAirspacePath.Set(at, "/tmp/somewhere");

            await Assert.That(await File.ReadAllTextAsync(at)).IsEqualTo("{ this is not json");
            await Assert.That(said).Contains("Environment", StringComparison.Ordinal)
                .Because("the refusal names where the whole document can be repaired, or a "
                       + "person is told no and not told where to go.");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(at)!, recursive: true);
        }
    }
}
