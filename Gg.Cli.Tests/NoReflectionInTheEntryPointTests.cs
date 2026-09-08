using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// The entry point suppresses a trim warning, so it may not produce one.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the check that makes the suppression narrow rather than merely
/// small.</b> <c>Gg.Cli.csproj</c> silences IL2072 because exactly two arrive,
/// both inside <c>Common.Logging</c>'s reflective logger-factory adapter, which
/// comes in transitively through WebSocket signalling this binary never uses.
/// A project-scoped <c>NoWarn</c> would also hide the same warning about OUR
/// code, and the argument for it being safe is "Gg.Cli is a thin entry point" —
/// which is a claim about the code, so it is asserted rather than believed.
/// </para>
/// <para>
/// <b>IL2072 is raised where an unannotated type flows into a reflective
/// call.</b> So the shapes below are the ones that can produce it, and a file
/// with none of them cannot be the source of a suppressed warning.
/// </para>
/// <para>
/// <b>The real fix is in the fork.</b> It already exists for an AOT reason;
/// dropping the WebSocketSharp dependency removes the warnings entirely and this
/// suppression with them. Until then this is what keeps the exception honest.
/// </para>
/// </remarks>
public class NoReflectionInTheEntryPointTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    private static IEnumerable<string> EntryPointSources() =>
        Directory.EnumerateFiles(Path.Combine(Root(), "Gg.Cli"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));

    /// <summary>The shapes that raise IL2072, by the call that raises it.</summary>
    private static readonly Regex Reflective = new(
        @"\b(Activator\.CreateInstance|Type\.GetType|Assembly\.Load|"
      + @"GetConstructor|GetMethod|GetProperty|GetField|MakeGenericType)\b",
        RegexOptions.Compiled);

    [Test]
    public async Task The_entry_point_reflects_on_nothing()
    {
        var offenders = EntryPointSources()
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .Where(x => Reflective.IsMatch(x.Text))
            .Select(x => x.File)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("Gg.Cli suppresses IL2072 for a third-party assembly, and that is only "
                   + "safe while nothing here can produce one. Either remove the reflection or "
                   + "remove the suppression. Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_suppression_says_what_it_is_for()
    {
        // A NoWarn WITH NO REASON IS A NoWarn NOBODY CAN REMOVE, because the
        // next person cannot tell whether the thing it hid was fixed.
        var project = File.ReadAllText(Path.Combine(Root(), "Gg.Cli", "Gg.Cli.csproj"));

        await Assert.That(project).Contains("IL2072");
        await Assert.That(project).Contains("Common.Logging")
            .Because("it has to name the assembly whose warnings it is hiding.");
        // CASE-INSENSITIVE, because this file writes its headings in capitals and
        // the requirement is that the removal path is NAMED - not that it is
        // spelled a particular way. A brittle assertion here would be a test
        // about house style wearing a test about safety.
        await Assert.That(project.Contains("fork", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("it has to say what would remove it, or it is permanent by default.");
    }

    [Test]
    public async Task The_scan_finds_reflection_that_is_really_there()
    {
        // The poison twin. Both assertions above pass on a regex that matches
        // nothing, and on an enumeration that found no files.
        await Assert.That(EntryPointSources().Any()).IsTrue();

        await Assert.That(Reflective.IsMatch("var x = Activator.CreateInstance(t);")).IsTrue();
        await Assert.That(Reflective.IsMatch("var t = Type.GetType(name);")).IsTrue();
        await Assert.That(Reflective.IsMatch("var x = thing.GetTypeName();")).IsFalse()
            .Because("a method that merely reads like reflection must not be flagged, or the "
                   + "rule becomes one people delete.");
    }
}
