using System.Xml.Linq;

namespace Gg.Cli.Tests;

/// <summary>
/// The command a person types, and the package that installs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>There was no way to obtain this binary except by building it.</b> Three
/// files said so in prose, and a pool host paid for it: the .NET SDK, a git
/// clone, and a platform linker on every machine that wanted to run a runner.
/// </para>
/// <para>
/// <b>The package id and the command are different names, and only one of them
/// is typed.</b> <c>gg</c> is taken on nuget.org, so the package is
/// <c>GlyphGuild.Gg.Cli</c> — matching <c>GlyphGuild.Gg.Contracts</c> — while
/// <c>ToolCommandName</c> keeps the command <c>gg</c>. Nothing else in the
/// repository would notice if that drifted: no test asserts the printed name,
/// and every script and doc would simply be wrong at once.
/// </para>
/// <para>
/// <b><c>PublishAot</c> stays, and is overridden only at pack time.</b> A tool
/// package is IL and a native binary is not, so the two cannot both apply to
/// one invocation. Leaving AOT as the project's default keeps
/// <c>dotnet publish</c> and CI's <c>aot</c> job behaving exactly as they did,
/// and confines the exception to the one command that needs it — asserted here
/// so that "just turn AOT off in the csproj" does not quietly become the fix.
/// </para>
/// </remarks>
public class PackagingTests
{
    private static XDocument CliProject() => XDocument.Load(RepoFile("Gg.Cli", "Gg.Cli.csproj"));

    private static string? Property(XDocument project, string name) => project
        .Descendants()
        .FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    [Test]
    public async Task The_cli_is_packable_as_a_tool()
    {
        var project = CliProject();

        await Assert.That(Property(project, "PackAsTool")).IsEqualTo("true")
            .Because("without this the package is a library nobody can install, and the only "
                   + "way to get gg stays 'clone it and build it'.");
        await Assert.That(Property(project, "IsPackable")).IsEqualTo("true");
    }

    [Test]
    public async Task The_command_is_gg_whatever_the_package_is_called()
    {
        // THE PAIR THAT MUST NOT DRIFT. The package id cannot be `gg` - that id
        // belongs to somebody else on nuget.org - so the two names differ on
        // purpose, and the one people type is the one nothing else asserts.
        var project = CliProject();

        await Assert.That(Property(project, "ToolCommandName")).IsEqualTo("gg")
            .Because("every script, doc and systemd unit in both repositories invokes `gg`. "
                   + "Renaming the command breaks all of them at once and no other test looks.");
        await Assert.That(Property(project, "PackageId")).IsEqualTo("GlyphGuild.Gg.Cli")
            .Because("it matches GlyphGuild.Gg.Contracts, and fixing it now is cheaper than "
                   + "discovering it after something is published under another name.");
    }

    [Test]
    public async Task The_package_declares_a_licence_because_warnings_are_errors()
    {
        // TreatWarningsAsErrors is repo-wide, and a pack with no licence raises
        // NU5125. So this is not metadata hygiene - without it the package does
        // not build at all.
        await Assert.That(Property(CliProject(), "PackageLicenseExpression")).IsEqualTo("MIT");
    }

    [Test]
    public async Task Native_publishing_is_still_the_projects_default()
    {
        // The binaries are what a machine with no .NET runtime installs, which
        // is every pool host. If AOT were turned off here to make packing work,
        // that would stop being true and the only sign would be a much smaller
        // release asset.
        await Assert.That(Property(CliProject(), "PublishAot")).IsEqualTo("true")
            .Because("a tool package needs the runtime; the native binary needs nothing. Pack "
                   + "overrides this per-invocation - it must not be removed from the project.");
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory)
            : Path.Combine([dir.FullName, .. parts]);
    }
}
